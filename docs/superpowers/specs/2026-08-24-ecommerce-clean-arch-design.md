# E-commerce Clean Architecture — Design Spec

## Purpose

A portfolio project demonstrating professional .NET architecture and practices:
Clean Architecture layering, EF Core, JWT authentication, and a layered
automated test suite. The domain (order management) is a vehicle for showing
the architecture, not the focus in itself — kept simple and well-known so an
evaluator can follow the business rules without background.

Two vanilla-JS frontends (storefront, admin) consume the same API, showing
both a public-facing and an authenticated flow against one backend.

## Architecture

```
ecommerce-clean-arch/
├── src/
│   ├── Domain/           # Entities, business rules, no external dependencies
│   ├── Application/      # Use-case handlers, repository interfaces, DTOs
│   ├── Infrastructure/   # EF Core, repository implementations, JWT auth
│   └── Api/               # Controllers, DI wiring, middleware
├── tests/
│   ├── Domain.UnitTests/
│   ├── Application.UnitTests/
│   └── Api.IntegrationTests/
├── frontend/
│   ├── storefront/       # HTML/JS/CSS vanilla — catalog, cart, checkout
│   └── admin/             # HTML/JS/CSS vanilla — login, orders, products, stock
```

**Dependency rule:** `Api` → `Application` → `Domain`. `Infrastructure`
implements interfaces defined in `Application`/`Domain` (dependency
inversion). `Domain` references nothing else in the solution.

## Entities

- **`Product`** — name, price, stock quantity.
- **`Order`** — line items (product, quantity, price at time of order),
  status (`Pending → Paid → Shipped`, or `→ Cancelled`).
- **`Customer`** — name, email (attached to an order at checkout; no login
  required for the storefront).
- **`AdminUser`** — username, password hash — the only entity that logs in.

## Data flow and business rules

### Storefront (public, no auth)

1. `GET /api/products` — lists the catalog with current stock.
2. The customer builds a cart entirely in frontend state (not persisted to
   the backend until checkout).
3. `POST /api/orders` — creates the order from the cart's line items. The
   `Application` layer validates every line item has sufficient stock; if
   any line fails, the whole order is rejected (no partial writes). On
   success, stock is decremented and the `Order` is created with status
   `Pending`, both in a single transaction.

### Admin (JWT-protected)

1. `POST /api/auth/login` — validates credentials, returns a JWT.
2. `GET /api/orders` — lists orders, filterable by status.
3. `PATCH /api/orders/{id}/status` — advances status
   (`Pending → Paid → Shipped`, or `→ Cancelled` from `Pending`/`Paid`).
   Invalid transitions (e.g. `Shipped → Pending`) are rejected. Cancelling a
   `Pending` or `Paid` order returns its reserved stock.
4. `POST /api/products`, `PUT /api/products/{id}`,
   `DELETE /api/products/{id}` — product CRUD, including stock adjustment.

### Error handling

The API returns `ProblemDetails` (RFC 7807) for all error responses:
validation failures → 400, business-rule violations (insufficient stock,
invalid status transition) → 422. Domain rules throw a `DomainException`
hierarchy; a single central middleware catches these and translates them to
the appropriate `ProblemDetails` response — controllers contain no manual
try/catch for business errors.

## Testing

- **`Domain.UnitTests`** — pure business rules, no mocks: `Order` rejects
  invalid transitions; stock never goes negative; `Order.Cancel()` returns
  reserved stock.
- **`Application.UnitTests`** — use-case handlers (e.g.
  `CreateOrderHandler`) tested against fake/in-memory repositories (no real
  database) — covers the happy path and rejections (insufficient stock,
  unknown product).
- **`Api.IntegrationTests`** — `WebApplicationFactory` boots the full API
  against a real SQLite in-memory database (not mocked) — covers JWT
  authentication, the main endpoints end-to-end, and the `ProblemDetails`
  shape/status codes of error responses.
- CI (GitHub Actions) runs all three test projects on every push/PR.

## Frontend

Both `storefront/` and `admin/` are vanilla JS + `fetch`, no framework, no
bundler — plain HTML/CSS/JS served statically, matching the project's scope.
`admin/` holds the JWT in memory/`sessionStorage` after login and attaches
it to every request. No business logic lives in the frontend beyond basic
form validation — every real rule lives in the API.
