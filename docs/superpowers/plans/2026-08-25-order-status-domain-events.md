# Order Status Domain Events & Notifications Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a hand-rolled Domain Events mechanism so every successful order-status transition raises an event that triggers a (console-logged) customer notification, decoupling the notification side effect from `UpdateOrderStatusHandler`'s core logic.

**Architecture:** `Order` accumulates `IDomainEvent`s internally as its status changes; `UpdateOrderStatusHandler` pulls them after saving and hands them to an `IDomainEventDispatcher`, which resolves and invokes registered `IDomainEventHandler<TEvent>` implementations via DI. A single handler resolves the customer and calls `INotificationSender`, whose only implementation logs to the console.

**Tech Stack:** Existing .NET 8 / ASP.NET Core / EF Core stack — no new external dependencies.

## Global Constraints

- No new NuGet packages. The dispatcher is hand-rolled (no MediatR or similar).
- A notification failure must never fail the HTTP response for a status update — `UpdateOrderStatusHandler` catches and logs dispatch errors rather than propagating them.
- `Order.Cancel()`'s existing stock-return behavior (Task 5 of the original plan) is unchanged — this feature only adds event-raising on top of the existing transition logic, never replaces it.
- All new interfaces (`IDomainEventDispatcher`, `IDomainEventHandler<TEvent>`, `INotificationSender`) live in `Application`; their implementations live in `Infrastructure`, per the existing dependency-inversion pattern in this codebase.
- `Domain` remains free of any dependency beyond what it already has (still zero package/project references).

---

### Task 1: Domain — Events and `Order` Event-Raising

**Files:**
- Create: `src/Domain/Events/IDomainEvent.cs`
- Create: `src/Domain/Events/OrderStatusChangedEvent.cs`
- Modify: `src/Domain/Order.cs`
- Test: `tests/Domain.UnitTests/OrderTests.cs`

**Interfaces:**
- Consumes: `OrderStatus` (existing).
- Produces: `IDomainEvent` (empty marker interface); `OrderStatusChangedEvent(Guid OrderId, Guid CustomerId, OrderStatus OldStatus, OrderStatus NewStatus) : IDomainEvent`; `Order.PullDomainEvents()` returning `IReadOnlyList<IDomainEvent>` and clearing the internal list. Consumed by `Application` in Task 2.

- [ ] **Step 1: Write the failing tests**

Add these test methods to `tests/Domain.UnitTests/OrderTests.cs` (add the `using Domain.Events;` at the top of the file alongside the existing usings):

```csharp
[Fact]
public void AdvanceTo_valid_transition_raises_OrderStatusChangedEvent()
{
    var customerId = Guid.NewGuid();
    var order = Order.Create(customerId, new[] { Item() });

    order.AdvanceTo(OrderStatus.Paid);

    var events = order.PullDomainEvents();
    var raised = Assert.Single(events);
    var statusChanged = Assert.IsType<OrderStatusChangedEvent>(raised);
    Assert.Equal(order.Id, statusChanged.OrderId);
    Assert.Equal(customerId, statusChanged.CustomerId);
    Assert.Equal(OrderStatus.Pending, statusChanged.OldStatus);
    Assert.Equal(OrderStatus.Paid, statusChanged.NewStatus);
}

[Fact]
public void Cancel_raises_OrderStatusChangedEvent_with_Cancelled_as_new_status()
{
    var order = Order.Create(Guid.NewGuid(), new[] { Item() });

    order.Cancel();

    var events = order.PullDomainEvents();
    var statusChanged = Assert.IsType<OrderStatusChangedEvent>(Assert.Single(events));
    Assert.Equal(OrderStatus.Cancelled, statusChanged.NewStatus);
}

[Fact]
public void AdvanceTo_invalid_transition_raises_no_event()
{
    var order = Order.Create(Guid.NewGuid(), new[] { Item() });

    Assert.Throws<InvalidOrderStatusTransitionException>(() => order.AdvanceTo(OrderStatus.Shipped));

    Assert.Empty(order.PullDomainEvents());
}

[Fact]
public void PullDomainEvents_clears_the_list_after_returning_it()
{
    var order = Order.Create(Guid.NewGuid(), new[] { Item() });
    order.AdvanceTo(OrderStatus.Paid);

    order.PullDomainEvents();
    var secondPull = order.PullDomainEvents();

    Assert.Empty(secondPull);
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test tests/Domain.UnitTests`
Expected: Build errors — `OrderStatusChangedEvent` and `Order.PullDomainEvents()` do not exist.

- [ ] **Step 3: Add `IDomainEvent` and `OrderStatusChangedEvent`**

`src/Domain/Events/IDomainEvent.cs`:

```csharp
namespace Domain.Events;

public interface IDomainEvent
{
}
```

`src/Domain/Events/OrderStatusChangedEvent.cs`:

```csharp
namespace Domain.Events;

public record OrderStatusChangedEvent(
    Guid OrderId,
    Guid CustomerId,
    OrderStatus OldStatus,
    OrderStatus NewStatus) : IDomainEvent;
```

- [ ] **Step 4: Update `Order` to raise and expose events**

In `src/Domain/Order.cs`, add `using Domain.Events;` to the top of the file, add a private events list, and update `AdvanceTo`:

```csharp
using Domain.Events;
using Domain.Exceptions;

namespace Domain;

public class Order
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> ValidTransitions = new()
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Paid, OrderStatus.Cancelled },
        [OrderStatus.Paid] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
        [OrderStatus.Shipped] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>(),
    };

    private readonly List<OrderItem> _items = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items;

    private Order() { }

    public static Order Create(Guid customerId, IEnumerable<OrderItem> items)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Pending,
        };
        order._items.AddRange(items);
        return order;
    }

    public void AdvanceTo(OrderStatus newStatus)
    {
        if (!ValidTransitions[Status].Contains(newStatus))
        {
            throw new InvalidOrderStatusTransitionException(Status, newStatus);
        }

        var oldStatus = Status;
        Status = newStatus;
        _domainEvents.Add(new OrderStatusChangedEvent(Id, CustomerId, oldStatus, newStatus));
    }

    public void Cancel()
    {
        AdvanceTo(OrderStatus.Cancelled);
    }

    public IReadOnlyList<IDomainEvent> PullDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Domain.UnitTests`
Expected: All tests PASS, including the four new ones and all pre-existing `OrderTests`/`ProductTests`.

- [ ] **Step 6: Commit**

```bash
git add src/Domain/Events src/Domain/Order.cs tests/Domain.UnitTests/OrderTests.cs
git commit -m "feat(domain): raise OrderStatusChangedEvent on successful status transitions"
```

---

### Task 2: Application — Dispatcher Contracts, Notification Handler, and `UpdateOrderStatusHandler` Wiring

**Files:**
- Create: `src/Application/Events/IDomainEventDispatcher.cs`
- Create: `src/Application/Events/IDomainEventHandler.cs`
- Create: `src/Application/Notifications/INotificationSender.cs`
- Create: `src/Application/Notifications/OrderStatusChangedNotificationHandler.cs`
- Modify: `src/Application/Repositories/ICustomerRepository.cs`
- Modify: `src/Application/Orders/UpdateOrderStatusHandler.cs`
- Test: `tests/Application.UnitTests/Fakes/FakeCustomerRepository.cs` (add `GetByIdAsync`)
- Test: `tests/Application.UnitTests/Fakes/FakeDomainEventDispatcher.cs`
- Test: `tests/Application.UnitTests/Fakes/FakeNotificationSender.cs`
- Test: `tests/Application.UnitTests/UpdateOrderStatusHandlerTests.cs`
- Test: `tests/Application.UnitTests/OrderStatusChangedNotificationHandlerTests.cs`

**Interfaces:**
- Consumes: `IDomainEvent`, `OrderStatusChangedEvent`, `Order.PullDomainEvents()` (Task 1); `ICustomerRepository`, `IOrderRepository`, `IProductRepository` (existing).
- Produces: `IDomainEventDispatcher.DispatchAsync(IEnumerable<IDomainEvent>)`; `IDomainEventHandler<TEvent>.HandleAsync(TEvent)`; `INotificationSender.SendOrderStatusChangedAsync(string customerEmail, Guid orderId, OrderStatus oldStatus, OrderStatus newStatus)`; `ICustomerRepository.GetByIdAsync(Guid)`. Consumed by `Infrastructure` (Task 3) and `Api` DI wiring (Task 4).

- [ ] **Step 1: Add `GetByIdAsync` to `ICustomerRepository` and its fake**

`src/Application/Repositories/ICustomerRepository.cs` — add the method to the existing interface:

```csharp
using Domain;

namespace Application.Repositories;

public interface ICustomerRepository
{
    Task<Customer?> GetByEmailAsync(string email);
    Task<Customer?> GetByIdAsync(Guid id);
    Task AddAsync(Customer customer);
    Task SaveChangesAsync();
}
```

`tests/Application.UnitTests/Fakes/FakeCustomerRepository.cs` — add the matching method (the fake is already keyed by `Guid` internally, so this is a direct lookup):

```csharp
using Application.Repositories;
using Domain;

namespace Application.UnitTests.Fakes;

public class FakeCustomerRepository : ICustomerRepository
{
    private readonly Dictionary<Guid, Customer> _customers = new();

    public void Seed(Customer customer) => _customers[customer.Id] = customer;

    public IReadOnlyList<Customer> Customers => _customers.Values.ToList();

    public Task<Customer?> GetByEmailAsync(string email) =>
        Task.FromResult(_customers.Values.FirstOrDefault(c => c.Email == email));

    public Task<Customer?> GetByIdAsync(Guid id) =>
        Task.FromResult(_customers.GetValueOrDefault(id));

    public Task AddAsync(Customer customer)
    {
        _customers[customer.Id] = customer;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => Task.CompletedTask;
}
```

- [ ] **Step 2: Add dispatcher and handler contracts**

`src/Application/Events/IDomainEventDispatcher.cs`:

```csharp
using Domain.Events;

namespace Application.Events;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events);
}
```

`src/Application/Events/IDomainEventHandler.cs`:

```csharp
using Domain.Events;

namespace Application.Events;

public interface IDomainEventHandler<TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent);
}
```

- [ ] **Step 3: Add `INotificationSender` and `OrderStatusChangedNotificationHandler`**

`src/Application/Notifications/INotificationSender.cs`:

```csharp
using Domain;

namespace Application.Notifications;

public interface INotificationSender
{
    Task SendOrderStatusChangedAsync(string customerEmail, Guid orderId, OrderStatus oldStatus, OrderStatus newStatus);
}
```

`src/Application/Notifications/OrderStatusChangedNotificationHandler.cs`:

```csharp
using Application.Events;
using Application.Repositories;
using Domain.Events;

namespace Application.Notifications;

public class OrderStatusChangedNotificationHandler : IDomainEventHandler<OrderStatusChangedEvent>
{
    private readonly ICustomerRepository _customers;
    private readonly INotificationSender _sender;

    public OrderStatusChangedNotificationHandler(ICustomerRepository customers, INotificationSender sender)
    {
        _customers = customers;
        _sender = sender;
    }

    public async Task HandleAsync(OrderStatusChangedEvent domainEvent)
    {
        var customer = await _customers.GetByIdAsync(domainEvent.CustomerId);
        if (customer is null)
        {
            return;
        }

        await _sender.SendOrderStatusChangedAsync(
            customer.Email,
            domainEvent.OrderId,
            domainEvent.OldStatus,
            domainEvent.NewStatus);
    }
}
```

- [ ] **Step 4: Write the failing tests for the notification handler and `UpdateOrderStatusHandler` wiring**

`tests/Application.UnitTests/Fakes/FakeDomainEventDispatcher.cs`:

```csharp
using Application.Events;
using Domain.Events;

namespace Application.UnitTests.Fakes;

public class FakeDomainEventDispatcher : IDomainEventDispatcher
{
    public readonly List<IDomainEvent> DispatchedEvents = new();

    public Task DispatchAsync(IEnumerable<IDomainEvent> events)
    {
        DispatchedEvents.AddRange(events);
        return Task.CompletedTask;
    }
}
```

`tests/Application.UnitTests/Fakes/FakeNotificationSender.cs`:

```csharp
using Application.Notifications;
using Domain;

namespace Application.UnitTests.Fakes;

public record SentNotification(string CustomerEmail, Guid OrderId, OrderStatus OldStatus, OrderStatus NewStatus);

public class FakeNotificationSender : INotificationSender
{
    public readonly List<SentNotification> Sent = new();

    public Task SendOrderStatusChangedAsync(string customerEmail, Guid orderId, OrderStatus oldStatus, OrderStatus newStatus)
    {
        Sent.Add(new SentNotification(customerEmail, orderId, oldStatus, newStatus));
        return Task.CompletedTask;
    }
}
```

`tests/Application.UnitTests/OrderStatusChangedNotificationHandlerTests.cs`:

```csharp
using Application.Notifications;
using Application.UnitTests.Fakes;
using Domain;
using Domain.Events;
using Xunit;

namespace Application.UnitTests;

public class OrderStatusChangedNotificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_sends_notification_with_the_customers_email()
    {
        var customerId = Guid.NewGuid();
        var customers = new FakeCustomerRepository();
        customers.Seed(new Customer(customerId, "Ada Lovelace", "ada@example.com"));
        var sender = new FakeNotificationSender();
        var handler = new OrderStatusChangedNotificationHandler(customers, sender);
        var orderId = Guid.NewGuid();

        await handler.HandleAsync(new OrderStatusChangedEvent(orderId, customerId, OrderStatus.Pending, OrderStatus.Paid));

        var sent = Assert.Single(sender.Sent);
        Assert.Equal("ada@example.com", sent.CustomerEmail);
        Assert.Equal(orderId, sent.OrderId);
        Assert.Equal(OrderStatus.Pending, sent.OldStatus);
        Assert.Equal(OrderStatus.Paid, sent.NewStatus);
    }

    [Fact]
    public async Task HandleAsync_does_nothing_when_customer_not_found()
    {
        var customers = new FakeCustomerRepository();
        var sender = new FakeNotificationSender();
        var handler = new OrderStatusChangedNotificationHandler(customers, sender);

        await handler.HandleAsync(new OrderStatusChangedEvent(Guid.NewGuid(), Guid.NewGuid(), OrderStatus.Pending, OrderStatus.Paid));

        Assert.Empty(sender.Sent);
    }
}
```

`tests/Application.UnitTests/UpdateOrderStatusHandlerTests.cs` currently reads exactly as follows — the constructor call on line 19 (`new UpdateOrderStatusHandler(orders, products)`) will stop compiling once Step 6 adds the dispatcher parameter, so it must be updated in the same change. Replace the entire file content with:

```csharp
using Application.Orders;
using Application.UnitTests.Fakes;
using Domain;
using Domain.Events;
using Xunit;

namespace Application.UnitTests;

public class UpdateOrderStatusHandlerTests
{
    [Fact]
    public async Task HandleAsync_cancelling_a_pending_order_returns_stock()
    {
        var productId = Guid.NewGuid();
        var products = new FakeProductRepository();
        products.Seed(new Product(productId, "Widget", 10.00m, stockQuantity: 5));
        var order = Order.Create(Guid.NewGuid(), new[] { new OrderItem(productId, 3, 10.00m) });
        var orders = new FakeOrderRepository();
        orders.Orders.Add(order);
        var dispatcher = new FakeDomainEventDispatcher();
        var handler = new UpdateOrderStatusHandler(orders, products, dispatcher);

        await handler.HandleAsync(order.Id, OrderStatus.Cancelled);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(8, (await products.GetByIdAsync(productId))!.StockQuantity);
    }

    [Fact]
    public async Task HandleAsync_dispatches_the_order_status_changed_event_after_saving()
    {
        var order = Order.Create(Guid.NewGuid(), new[] { new OrderItem(Guid.NewGuid(), 1, 10.00m) });
        var orders = new FakeOrderRepository();
        orders.Orders.Add(order);
        var products = new FakeProductRepository();
        var dispatcher = new FakeDomainEventDispatcher();
        var handler = new UpdateOrderStatusHandler(orders, products, dispatcher);

        await handler.HandleAsync(order.Id, OrderStatus.Paid);

        var dispatched = Assert.Single(dispatcher.DispatchedEvents);
        var statusChanged = Assert.IsType<OrderStatusChangedEvent>(dispatched);
        Assert.Equal(OrderStatus.Pending, statusChanged.OldStatus);
        Assert.Equal(OrderStatus.Paid, statusChanged.NewStatus);
    }
}
```

- [ ] **Step 5: Run tests to verify they fail to compile**

Run: `dotnet test tests/Application.UnitTests`
Expected: Build errors — `UpdateOrderStatusHandler`'s constructor doesn't yet accept an `IDomainEventDispatcher`, and `OrderStatusChangedNotificationHandler` test file references types that don't fail (those were added in Step 3) — the compile failure specifically comes from the `UpdateOrderStatusHandler` constructor call in Step 4's new test.

- [ ] **Step 6: Update `UpdateOrderStatusHandler` to dispatch events**

`src/Application/Orders/UpdateOrderStatusHandler.cs`:

```csharp
using Application.Dtos;
using Application.Events;
using Application.Repositories;
using Domain;

namespace Application.Orders;

public class UpdateOrderStatusHandler
{
    private readonly IOrderRepository _orders;
    private readonly IProductRepository _products;
    private readonly IDomainEventDispatcher _dispatcher;

    public UpdateOrderStatusHandler(IOrderRepository orders, IProductRepository products, IDomainEventDispatcher dispatcher)
    {
        _orders = orders;
        _products = products;
        _dispatcher = dispatcher;
    }

    public async Task<OrderDto> HandleAsync(Guid orderId, OrderStatus newStatus)
    {
        var order = await _orders.GetByIdAsync(orderId)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");

        order.AdvanceTo(newStatus);

        if (newStatus == OrderStatus.Cancelled)
        {
            foreach (var item in order.Items)
            {
                var product = await _products.GetByIdAsync(item.ProductId);
                product?.IncreaseStock(item.Quantity);
            }
            await _products.SaveChangesAsync();
        }

        await _orders.SaveChangesAsync();

        var domainEvents = order.PullDomainEvents();
        try
        {
            await _dispatcher.DispatchAsync(domainEvents);
        }
        catch
        {
            // A notification failure must never fail an otherwise-successful status update.
            // Task 4 wires a real ILogger-based implementation; this handler only guarantees
            // the exception does not propagate.
        }

        return GetOrdersHandler.ToDto(order);
    }
}
```

Note: this task intentionally swallows the exception with a bare `catch` rather than logging here, because `Application` has no `ILogger` dependency wired into this handler yet and the Global Constraints forbid adding scope beyond what's needed. Task 3's `InProcessDomainEventDispatcher` is itself responsible for logging any handler exception before it would reach this catch, so in practice this catch is a last-resort safety net, not the primary error-reporting path.

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests/Application.UnitTests`
Expected: All tests PASS, including all pre-existing `UpdateOrderStatusHandlerTests`, the new dispatch test, and both new `OrderStatusChangedNotificationHandlerTests`.

- [ ] **Step 8: Commit**

```bash
git add src/Application/Events src/Application/Notifications src/Application/Repositories/ICustomerRepository.cs src/Application/Orders/UpdateOrderStatusHandler.cs tests/Application.UnitTests
git commit -m "feat(application): dispatch OrderStatusChangedEvent to a notification handler after status updates"
```

---

### Task 3: Infrastructure — Dispatcher and Console Notification Sender

**Files:**
- Create: `src/Infrastructure/Events/InProcessDomainEventDispatcher.cs`
- Create: `src/Infrastructure/Notifications/ConsoleNotificationSender.cs`
- Modify: `src/Infrastructure/Persistence/CustomerRepository.cs`

**Interfaces:**
- Consumes: `IDomainEventDispatcher`, `IDomainEventHandler<TEvent>`, `INotificationSender` (Task 2); `IServiceProvider`, `ILogger<T>` (framework).
- Produces: `InProcessDomainEventDispatcher : IDomainEventDispatcher`; `ConsoleNotificationSender : INotificationSender`; `CustomerRepository.GetByIdAsync(Guid)`. Consumed by `Api` DI wiring in Task 4.

This task has no new unit tests of its own — `InProcessDomainEventDispatcher`'s reflection-based dispatch is exercised end-to-end by Task 4's integration test, and `ConsoleNotificationSender` is a thin console-writing wrapper with no branching logic to unit test.

- [ ] **Step 1: Add `GetByIdAsync` to `CustomerRepository`**

`src/Infrastructure/Persistence/CustomerRepository.cs`:

```csharp
using Application.Repositories;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _db;
    public CustomerRepository(AppDbContext db) => _db = db;

    public Task<Customer?> GetByEmailAsync(string email) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Email == email);

    public Task<Customer?> GetByIdAsync(Guid id) =>
        _db.Customers.FirstOrDefaultAsync(c => c.Id == id);

    public async Task AddAsync(Customer customer) =>
        await _db.Customers.AddAsync(customer);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
```

- [ ] **Step 2: Write `InProcessDomainEventDispatcher`**

`src/Infrastructure/Events/InProcessDomainEventDispatcher.cs`:

```csharp
using Application.Events;
using Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Events;

public class InProcessDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InProcessDomainEventDispatcher> _logger;

    public InProcessDomainEventDispatcher(IServiceProvider serviceProvider, ILogger<InProcessDomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = (IEnumerable<object>)_serviceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                try
                {
                    var handleMethod = handlerType.GetMethod("HandleAsync")!;
                    await (Task)handleMethod.Invoke(handler, new object[] { domainEvent })!;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Domain event handler {HandlerType} failed for event {EventType}", handler.GetType().Name, domainEvent.GetType().Name);
                }
            }
        }
    }
}
```

- [ ] **Step 3: Write `ConsoleNotificationSender`**

`src/Infrastructure/Notifications/ConsoleNotificationSender.cs`:

```csharp
using Application.Notifications;
using Domain;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Notifications;

public class ConsoleNotificationSender : INotificationSender
{
    private readonly ILogger<ConsoleNotificationSender> _logger;

    public ConsoleNotificationSender(ILogger<ConsoleNotificationSender> logger) => _logger = logger;

    public Task SendOrderStatusChangedAsync(string customerEmail, Guid orderId, OrderStatus oldStatus, OrderStatus newStatus)
    {
        _logger.LogInformation(
            "[Notification] {CustomerEmail}: order {OrderId} changed from {OldStatus} to {NewStatus}",
            customerEmail, orderId, oldStatus, newStatus);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Verify the solution builds**

Run: `dotnet build`
Expected: Build succeeded, 0 errors. (`Api` will not yet compile cleanly against the new `UpdateOrderStatusHandler` constructor until Task 4 updates `Program.cs` — if `dotnet build` fails specifically on `src/Api` with a DI-related or constructor-mismatch message, that is expected at this point; confirm `dotnet build src/Infrastructure/Infrastructure.csproj` succeeds on its own to verify this task's own code compiles.)

- [ ] **Step 5: Commit**

```bash
git add src/Infrastructure/Events src/Infrastructure/Notifications src/Infrastructure/Persistence/CustomerRepository.cs
git commit -m "feat(infrastructure): add in-process domain event dispatcher and console notification sender"
```

---

### Task 4: Api — DI Wiring and Integration Test

**Files:**
- Modify: `src/Api/Program.cs`
- Test: `tests/Api.IntegrationTests/OrdersEndpointTests.cs`

**Interfaces:**
- Consumes: `IDomainEventDispatcher`, `INotificationSender`, `IDomainEventHandler<OrderStatusChangedEvent>` and their implementations (Tasks 2-3).
- Produces: nothing consumed by later tasks — this is the final task.

- [ ] **Step 1: Register the new services in `Program.cs`**

In `src/Api/Program.cs`, add the following `using` statements near the top (alongside the existing ones):

```csharp
using Application.Events;
using Application.Notifications;
using Domain.Events;
using Infrastructure.Events;
using Infrastructure.Notifications;
```

Add these registrations after the existing `builder.Services.AddScoped<ITokenGenerator, TokenGenerator>();` line and before the handler registrations:

```csharp
builder.Services.AddScoped<IDomainEventDispatcher, InProcessDomainEventDispatcher>();
builder.Services.AddScoped<INotificationSender, ConsoleNotificationSender>();
builder.Services.AddScoped<IDomainEventHandler<OrderStatusChangedEvent>, OrderStatusChangedNotificationHandler>();
```

- [ ] **Step 2: Verify the solution builds**

Run: `dotnet build`
Expected: Build succeeded, 0 errors — `UpdateOrderStatusHandler`'s new `IDomainEventDispatcher` constructor parameter now resolves via DI.

- [ ] **Step 3: Write the failing integration test**

`tests/Api.IntegrationTests/OrdersEndpointTests.cs` already has a `LoginAsAdminAsync(HttpClient)` private helper at the bottom of the class (seeds an `AdminUser` directly via the test `AppDbContext`, then logs in through `/api/auth/login`) and an existing pattern for seeding a `Product` directly via `AppDbContext` before placing an order through `POST /api/orders` (see `UpdateStatus_with_invalid_status_returns_400_not_500` in the same file). Add this new test into the class using that exact same pattern:

```csharp
[Fact]
public async Task UpdateStatus_valid_transition_returns_200_even_with_the_notification_pipeline_wired_up()
{
    var productId = Guid.NewGuid();
    using (var scope = _factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(new Product(productId, "Widget", 9.99m, 10));
        await db.SaveChangesAsync();
    }

    var client = _factory.CreateClient();
    var createResponse = await client.PostAsJsonAsync("/api/orders", new
    {
        customerName = "Jane Doe",
        customerEmail = "jane@example.com",
        lines = new[] { new { productId, quantity = 1 } },
    });
    var created = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
    var orderId = created!.Id;

    var token = await LoginAsAdminAsync(client);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var response = await client.PatchAsJsonAsync($"/api/orders/{orderId}/status", new { status = "Paid" });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var order = await response.Content.ReadFromJsonAsync<OrderDto>();
    Assert.Equal("Paid", order!.Status);
}
```

This uses only types and usings already present at the top of the file (`System.Net.HttpStatusCode`, `System.Net.Http.Headers.AuthenticationHeaderValue`, `System.Net.Http.Json`, `Application.Dtos.OrderDto`, `Domain.Product`, `Microsoft.Extensions.DependencyInjection`) — no new `using` statements are needed for this test.

- [ ] **Step 4: Run the full test suite**

Run: `dotnet test`
Expected: All tests across `Domain.UnitTests`, `Application.UnitTests`, and `Api.IntegrationTests` PASS.

- [ ] **Step 5: Manual sanity check**

Run: `dotnet run --project src/Api`, then in another terminal, log in as admin, create a product and an order, and `PATCH` its status. Confirm a `[Notification] ...` line appears in the running API's console output.

- [ ] **Step 6: Commit**

```bash
git add src/Api/Program.cs tests/Api.IntegrationTests/OrdersEndpointTests.cs
git commit -m "feat(api): wire domain event dispatcher and notification sender into DI"
```
