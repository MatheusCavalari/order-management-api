# E-commerce Clean Architecture

A portfolio project demonstrating Clean Architecture in ASP.NET Core:
layered Domain/Application/Infrastructure/Api projects, EF Core, JWT
authentication, and a fully layered automated test suite, fronted by two
vanilla-JS apps (storefront and admin) against the same API.

## Architecture

- **Domain** — entities and business rules, zero dependencies.
- **Application** — use-case handlers and repository interfaces.
- **Infrastructure** — EF Core (SQLite), JWT token generation, password hashing.
- **Api** — controllers, DI wiring, centralized `ProblemDetails` error handling.

## Running locally

    dotnet restore
    dotnet ef database update --project src/Infrastructure --startup-project src/Api
    dotnet run --project src/Api

The API seeds an admin user (`admin` / `changeme`) and three sample products
on first run in Development.

Open `frontend/storefront/index.html` and `frontend/admin/index.html`
directly in a browser (no build step) once the API is running.

## Testing

    dotnet test

- `Domain.UnitTests` — business rules (stock, order status transitions).
- `Application.UnitTests` — use-case handlers against fake repositories.
- `Api.IntegrationTests` — full API against an in-memory SQLite database.

## Tech stack

.NET 8, ASP.NET Core, EF Core, JWT Bearer auth, xUnit, vanilla JavaScript.
