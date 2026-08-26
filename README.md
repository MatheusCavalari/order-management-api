# Order Management API

[![CI](https://github.com/MatheusCavalari/order-management-api/actions/workflows/ci.yml/badge.svg)](https://github.com/MatheusCavalari/order-management-api/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](ecommerce-clean-arch.sln)

An order-management REST API built to demonstrate Clean Architecture in
ASP.NET Core: strict layering (Domain → Application → Infrastructure/Api),
EF Core persistence, JWT-protected admin endpoints, and a fully layered
automated test suite — fronted by two vanilla-JS apps (a public storefront
and an authenticated admin panel) that consume the same API.

The domain (products, orders, stock, order-status transitions) is
deliberately simple and well-known. The point of the project is the
architecture around it, not the business complexity.

## Architecture

```
src/
├── Domain/           entities and business rules — zero dependencies
├── Application/       use-case handlers, repository interfaces, DTOs
├── Infrastructure/   EF Core (SQLite), JWT token generation, password hashing
└── Api/               controllers, DI wiring, centralized ProblemDetails error handling
```

Dependency rule: `Api → Application → Domain`, with `Infrastructure`
implementing interfaces owned by `Application`/`Domain` (dependency
inversion) — `Domain` has no package or project references at all.

Every business-rule violation throws a `DomainException` subtype, caught by
a single middleware and translated into an [RFC 7807](https://www.rfc-editor.org/rfc/rfc7807)
`ProblemDetails` response (`422` for business-rule violations, `400` for
malformed requests).

## API

| Method | Route | Auth | Description |
|---|---|---|---|
| `GET` | `/api/products` | — | List the catalog |
| `POST` | `/api/products` | JWT | Create a product |
| `PUT` | `/api/products/{id}` | JWT | Update a product |
| `DELETE` | `/api/products/{id}` | JWT | Delete a product |
| `POST` | `/api/orders` | — | Place an order (validates and reserves stock atomically) |
| `GET` | `/api/orders` | JWT | List orders, optionally filtered by status |
| `GET` | `/api/orders/{id}` | JWT | Get a single order |
| `PATCH` | `/api/orders/{id}/status` | JWT | Advance an order's status |
| `POST` | `/api/auth/login` | — | Exchange admin credentials for a JWT |

Order status follows a fixed state machine: `Pending → Paid → Shipped`, with
`Pending`/`Paid` also able to move to `Cancelled` (which returns reserved
stock). Every other transition is rejected.

## Running locally

```bash
dotnet restore
dotnet ef database update --project src/Infrastructure --startup-project src/Api
dotnet run --project src/Api
```

The API seeds an admin user (`admin` / `changeme`) and three sample products
on first run in Development, and listens on `http://localhost:5007` (see
`src/Api/Properties/launchSettings.json`).

Open `frontend/storefront/index.html` and `frontend/admin/index.html`
directly in a browser (no build step, no bundler) once the API is running.

## Testing

```bash
dotnet test
```

| Project | Verifies |
|---|---|
| `Domain.UnitTests` | Business rules in isolation — stock invariants, order-status transitions — no mocks |
| `Application.UnitTests` | Use-case handlers against fake repositories |
| `Api.IntegrationTests` | The full API against a real SQLite database via `WebApplicationFactory` |

CI (GitHub Actions) runs the full suite on every push and pull request.

## Tech stack

- **ASP.NET Core** (.NET 8) — Web API, Controllers
- **EF Core** (SQLite) — persistence, code-first migrations
- **JWT Bearer auth** — admin-only endpoints, PBKDF2 password hashing
- **xUnit** — unit and integration tests
- **Vanilla JavaScript** — both frontends, no framework, no build step

## Concurrency Control

Stock quantities are protected against concurrent order placement using optimistic concurrency control:

- **RowVersion:** Each `Product` entity has an EF Core concurrency token (`RowVersion`/`byte[]`) that EF Core increments on every save.
- **Conflict Detection:** When multiple orders attempt to decrement the same product's stock simultaneously, EF Core detects that the `RowVersion` has changed since the order handler last read the product, and throws `DbUpdateConcurrencyException`.
- **Bounded Retry:** `CreateOrderHandler` catches the exception and retries the entire order-placement operation up to 3 times, allowing transient conflicts to resolve as one order completes before the next reads the product.
- **Result:** Two concurrent orders for the same product are serialized naturally by SQLite's locking; if stock is exhausted, the second order fails with a 422 Unprocessable Entity response.

## License

[MIT](LICENSE)
