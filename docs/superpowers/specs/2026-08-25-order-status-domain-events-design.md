# Order Status Domain Events & Notifications — Design Spec

## Purpose

Add a hand-rolled Domain Events mechanism to the order-management API: every
successful order-status transition raises an event, which triggers a
customer notification. The goal is to demonstrate the Domain Events pattern
itself (not just wire up a library like MediatR) as a natural extension of
the project's existing Clean Architecture, decoupling the notification side
effect from the core status-transition business logic.

Notifications are logged to the console (no real email delivery) — the
point is the architecture, not the delivery channel. All status
transitions notify (`Pending→Paid`, `Paid→Shipped`, and any `→Cancelled`).

## Architecture

### Domain

- `IDomainEvent` — empty marker interface.
- `OrderStatusChangedEvent(Guid OrderId, Guid CustomerId, OrderStatus OldStatus, OrderStatus NewStatus) : IDomainEvent` — a record.
- `Order` gains a private `List<IDomainEvent> _domainEvents` field.
  - `AdvanceTo(OrderStatus newStatus)` — after a transition succeeds (i.e.
    after the existing `ValidTransitions` check passes and `Status` is
    updated), appends a new `OrderStatusChangedEvent` to `_domainEvents`
    with the old and new status. An invalid transition still throws
    `InvalidOrderStatusTransitionException` and raises no event (unchanged
    behavior).
  - `Cancel()` calls `AdvanceTo(OrderStatus.Cancelled)`, so it raises an
    event through the same path — no separate handling needed.
  - `PullDomainEvents()` — returns the accumulated events as an
    `IReadOnlyList<IDomainEvent>` and clears the internal list. Called
    exactly once per handler invocation, after the entity is saved.

### Application

- `IDomainEventDispatcher` — `Task DispatchAsync(IEnumerable<IDomainEvent> events)`.
- `IDomainEventHandler<TEvent> where TEvent : IDomainEvent` — `Task HandleAsync(TEvent domainEvent)`.
- `OrderStatusChangedNotificationHandler : IDomainEventHandler<OrderStatusChangedEvent>` —
  looks up the `Customer` via `ICustomerRepository.GetByIdAsync(event.CustomerId)`
  (a new method on `ICustomerRepository`, mirroring the existing `GetByEmailAsync`),
  then calls `INotificationSender.SendOrderStatusChangedAsync(customer.Email, event.OrderId, event.OldStatus, event.NewStatus)`.
- `INotificationSender` — `Task SendOrderStatusChangedAsync(string customerEmail, Guid orderId, OrderStatus oldStatus, OrderStatus newStatus)`.
- `UpdateOrderStatusHandler.HandleAsync`, after `_orders.SaveChangesAsync()`
  and the existing stock-return logic, calls `order.PullDomainEvents()` and
  passes the result to `IDomainEventDispatcher.DispatchAsync(...)`. This
  dispatch is wrapped in a try/catch that logs any exception rather than
  rethrowing — a notification failure must never fail an otherwise-successful
  status update.

### Infrastructure

- `InProcessDomainEventDispatcher : IDomainEventDispatcher` — for each event,
  resolves `IEnumerable<IDomainEventHandler<TEvent>>` from the DI container
  (via reflection on the event's runtime type to build the closed generic
  handler type) and invokes each registered handler.
- `ConsoleNotificationSender : INotificationSender` — writes a line to the
  console/log, e.g. `[Notification] {email}: order {orderId} changed from {oldStatus} to {newStatus}`.

### Wiring (Api / Program.cs)

- Register `IDomainEventDispatcher → InProcessDomainEventDispatcher`,
  `INotificationSender → ConsoleNotificationSender`, and
  `IDomainEventHandler<OrderStatusChangedEvent> → OrderStatusChangedNotificationHandler`
  as scoped services.

## Error handling

A notification failure (e.g. an exception inside a handler) is caught in
`UpdateOrderStatusHandler` and logged via `ILogger`, but does not affect the
HTTP response — the order status change already succeeded and was saved
before dispatch runs. The API returns the same `OrderDto` it already
returns today; there is no change to the response shape.

## Testing

- **Domain.UnitTests**: `Order.AdvanceTo()` appends the expected event on a
  valid transition; `Order.Cancel()` also raises an event (via the same
  path); an invalid transition raises no event; `PullDomainEvents()`
  returns the accumulated events and leaves the internal list empty on a
  second call.
- **Application.UnitTests**: a fake `IDomainEventDispatcher` proves
  `UpdateOrderStatusHandler` calls `DispatchAsync` with the correct event
  after a successful status change; `OrderStatusChangedNotificationHandler`
  is tested against a fake `ICustomerRepository` and fake
  `INotificationSender`, confirming it resolves the right customer email
  and calls the sender with the right arguments.
- **Api.IntegrationTests**: one end-to-end test confirms a valid
  `PATCH /api/orders/{id}/status` still returns 200 with the correct
  `OrderDto` when the full dispatch pipeline (real DI-wired dispatcher,
  real console sender) runs in the background — proving the event
  machinery doesn't break the existing endpoint contract.

## Out of scope

- Real email delivery (SMTP, third-party providers) — logged to console only.
- Persisting a notification/audit log to the database.
- Notifying on order creation (`Pending` is the initial state, not a
  transition, so no event fires for it).
- Retry logic for failed notifications.
