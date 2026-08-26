# Optimistic Concurrency Control for Stock Management — Design Spec

## Purpose

Add optimistic concurrency control to `Product.StockQuantity` to prevent stock overselling when multiple orders are placed simultaneously. Two concurrent orders for the same product will no longer result in lost updates to stock — the second order will detect the conflict and retry.

The implementation uses EF Core's native concurrency tokens (RowVersion) and automatic conflict detection, with bounded retry logic in the Application layer.

## Problem

Currently, when two orders for the same product arrive within milliseconds:

1. Order A reads Product (Stock = 10)
2. Order B reads Product (Stock = 10)
3. Order A decrements to 9, saves
4. Order B decrements to 9, saves
5. Result: Stock is 9, but two units were sold (lost update)

The domain guarantees stock invariants at the single-request level (`Product.DecreaseStock()` throws `InsufficientStockException` if quantity > available). But concurrent requests bypass that protection.

## Architecture

### Domain

- `Product.cs` gains a `byte[]? RowVersion` property (nullable, initialized by EF on first save).
- No domain-layer logic changes — the entity doesn't "know" about concurrency; EF Core manages RowVersion transparently via the data access layer.
- `Product.DecreaseStock(int quantity)` and `Product.IncreaseStock(int quantity)` remain unchanged — they guard invariants, not concurrency.

### Application

- `CreateOrderHandler.HandleAsync(...)` catches `DbUpdateConcurrencyException` after `_orders.SaveChangesAsync()` and `_products.SaveChangesAsync()` when stock decrements fail due to conflicts.
- Bounded retry: on conflict, reload the product, re-validate stock is still available, retry the entire order-placement attempt. Maximum 3 attempts per order.
- If all 3 attempts fail (stock exhausted or other persistent conflict), throw a business exception (e.g., `OrderCreationFailedException` or reuse `InsufficientStockException` with a clear message) that the API layer translates to 409 Conflict or 422 Unprocessable Entity.

### Infrastructure

- EF Core's automatic concurrency detection requires no explicit code — adding `RowVersion` to the entity configuration and annotating it with `[ConcurrencyCheck]` or via `modelBuilder.Entity<Product>().Property(p => p.RowVersion).IsRowVersion()` is sufficient.
- EF Core increments RowVersion on every save, and detects conflicts when the saved RowVersion doesn't match the tracked value.

### Wiring (Api / Program.cs)

- No new registrations or middleware needed — conflict handling happens in `CreateOrderHandler`.

## Error Handling

A `DbUpdateConcurrencyException` during stock decrement is NOT a fatal error — it's a transient conflict. The handler retries. Only after 3 failed attempts is it treated as a permanent failure.

From the API consumer's perspective:
- Most concurrent orders succeed on the first or second attempt (conflict is rare and brief).
- If an order truly conflicts with too many others in rapid succession, a 409 or 422 response indicates the product is oversold and the order should be retried by the client or rejected.

## Testing

- **Domain.UnitTests**: No new tests — `Product.DecreaseStock()` behavior is unchanged.
- **Application.UnitTests**: 
  - `CreateOrderHandler` with a fake repo that simulates `DbUpdateConcurrencyException` on the first call, then succeeds — confirms retry logic works.
  - Verify that after 3 retries, the handler stops and throws a business exception.
- **Api.IntegrationTests**:
  - Real concurrency test: launch two tasks, each placing an order for the same product (stock = 2) simultaneously. Both should succeed, and stock should end at 0 — no oversell.
  - Real concurrency test: launch two tasks placing orders for stock = 1. One should succeed, one should fail with a 409 or 422 — no oversell.

## Out of Scope

- Distributed transactions across multiple databases — this only handles concurrency within a single SQLite connection/transaction scope.
- Optimistic concurrency for `Order` or other entities — only `Product.StockQuantity` is protected.
- Exponential backoff or jitter in retries — simple, bounded retry (3 attempts) is sufficient for a portfolio piece.
- Deadlock prevention — SQLite's simple locking model makes deadlocks unlikely; if they occur, they'll surface in testing.

## Implementation Strategy

1. Add `RowVersion` property to `Product`.
2. Update `ProductConfiguration` to annotate RowVersion as `IsRowVersion()`.
3. Create a migration.
4. Update `CreateOrderHandler` to wrap stock-decrement-and-save in a retry loop (3 attempts).
5. Add unit tests for retry logic and edge cases.
6. Add integration tests for real concurrent order placement.
