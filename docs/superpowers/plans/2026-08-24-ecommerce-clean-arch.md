# E-commerce Clean Architecture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a portfolio-quality order-management system in ASP.NET Core using Clean Architecture (Domain/Application/Infrastructure/Api), backed by EF Core + SQLite, JWT-protected admin endpoints, a layered automated test suite, and two vanilla-JS frontends (storefront, admin) consuming the same API.

**Architecture:** Four .NET class libraries wired by dependency inversion (`Api` → `Application` → `Domain`; `Infrastructure` implements `Application`/`Domain` interfaces), three matching test projects, and two static-file vanilla-JS frontends served independently of the API.

**Tech Stack:** .NET 8, ASP.NET Core Web API (Controllers), EF Core (SQLite provider), xUnit, JWT Bearer auth (`Microsoft.AspNetCore.Authentication.JwtBearer`), vanilla JS + `fetch`, GitHub Actions.

## Global Constraints

- Target framework: `net8.0` for every project.
- `Domain` project has zero NuGet package references and zero project references — pure C#.
- All money values are `decimal`, never `double`/`float`.
- Every business-rule violation throws a subtype of `DomainException`; controllers never manually catch business exceptions — the central middleware (Task 9) does.
- Error responses use RFC 7807 `ProblemDetails`: validation failures → 400, business-rule violations → 422.
- Order status values: `Pending`, `Paid`, `Shipped`, `Cancelled`. Valid transitions: `Pending → Paid`, `Paid → Shipped`, `Pending → Cancelled`, `Paid → Cancelled`. Every other transition is invalid.
- Cancelling a `Pending` or `Paid` order returns every line item's quantity to `Product.StockQuantity`.
- JWT settings (`Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey`, `Jwt:ExpiryMinutes`) come from configuration (`appsettings.json` for dev defaults, environment variables in deploy) — never hardcoded in source.
- Frontends (`frontend/storefront`, `frontend/admin`) are plain HTML/CSS/JS, no build step, no framework, no npm dependency — open directly or serve via any static file server.
- Solution file: `ecommerce-clean-arch.sln` at repo root.

---

### Task 1: Solution and Project Scaffold

**Files:**
- Create: `ecommerce-clean-arch.sln`
- Create: `src/Domain/Domain.csproj`
- Create: `src/Application/Application.csproj`
- Create: `src/Infrastructure/Infrastructure.csproj`
- Create: `src/Api/Api.csproj`
- Create: `tests/Domain.UnitTests/Domain.UnitTests.csproj`
- Create: `tests/Application.UnitTests/Application.UnitTests.csproj`
- Create: `tests/Api.IntegrationTests/Api.IntegrationTests.csproj`
- Create: `.gitignore`

**Interfaces:**
- Produces: the project graph every later task builds on. `Domain` has no references. `Application` references `Domain`. `Infrastructure` references `Application` and `Domain`. `Api` references `Application` and `Infrastructure`. Each test project references the `src` project it tests plus `Domain`/`Application` as needed for fakes.

- [ ] **Step 1: Create the solution and class library projects**

```bash
dotnet new sln -n ecommerce-clean-arch

dotnet new classlib -n Domain -o src/Domain -f net8.0
dotnet new classlib -n Application -o src/Application -f net8.0
dotnet new classlib -n Infrastructure -o src/Infrastructure -f net8.0
dotnet new webapi -n Api -o src/Api -f net8.0 --use-controllers

dotnet new xunit -n Domain.UnitTests -o tests/Domain.UnitTests -f net8.0
dotnet new xunit -n Application.UnitTests -o tests/Application.UnitTests -f net8.0
dotnet new xunit -n Api.IntegrationTests -o tests/Api.IntegrationTests -f net8.0

rm src/Domain/Class1.cs src/Application/Class1.cs src/Infrastructure/Class1.cs
```

- [ ] **Step 2: Add all projects to the solution**

```bash
dotnet sln add src/Domain/Domain.csproj src/Application/Application.csproj src/Infrastructure/Infrastructure.csproj src/Api/Api.csproj tests/Domain.UnitTests/Domain.UnitTests.csproj tests/Application.UnitTests/Application.UnitTests.csproj tests/Api.IntegrationTests/Api.IntegrationTests.csproj
```

- [ ] **Step 3: Wire project references**

```bash
dotnet add src/Application/Application.csproj reference src/Domain/Domain.csproj
dotnet add src/Infrastructure/Infrastructure.csproj reference src/Application/Application.csproj src/Domain/Domain.csproj
dotnet add src/Api/Api.csproj reference src/Application/Application.csproj src/Infrastructure/Infrastructure.csproj

dotnet add tests/Domain.UnitTests/Domain.UnitTests.csproj reference src/Domain/Domain.csproj
dotnet add tests/Application.UnitTests/Application.UnitTests.csproj reference src/Application/Application.csproj src/Domain/Domain.csproj
dotnet add tests/Api.IntegrationTests/Api.IntegrationTests.csproj reference src/Api/Api.csproj
```

- [ ] **Step 4: Add `.gitignore`**

```
bin/
obj/
*.user
.vs/
appsettings.Development.json
ecommerce.db
```

- [ ] **Step 5: Verify the solution builds and empty test suites run**

Run: `dotnet build`
Expected: Build succeeded, 0 errors (unused-`Class1` warnings are gone since those files were removed).

Run: `dotnet test`
Expected: 3 test projects run, 0 tests found in each (no test files yet), overall success.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution and project structure"
```

---

### Task 2: Domain Entities and Business Rules

**Files:**
- Create: `src/Domain/Exceptions/DomainException.cs`
- Create: `src/Domain/Exceptions/InsufficientStockException.cs`
- Create: `src/Domain/Exceptions/InvalidOrderStatusTransitionException.cs`
- Create: `src/Domain/Product.cs`
- Create: `src/Domain/Customer.cs`
- Create: `src/Domain/OrderStatus.cs`
- Create: `src/Domain/OrderItem.cs`
- Create: `src/Domain/Order.cs`
- Create: `src/Domain/AdminUser.cs`
- Test: `tests/Domain.UnitTests/OrderTests.cs`
- Test: `tests/Domain.UnitTests/ProductTests.cs`

**Interfaces:**
- Consumes: nothing (Domain has no dependencies).
- Produces: `Product.Id/Name/Price/StockQuantity`, `Product.DecreaseStock(int)`, `Product.IncreaseStock(int)`; `Order.Id/CustomerId/Status/Items` (`IReadOnlyList<OrderItem>`), `Order.Create(Guid customerId, IEnumerable<OrderItem> items)` (static factory, status `Pending`), `Order.AdvanceTo(OrderStatus)`, `Order.Cancel()`; `OrderItem.ProductId/Quantity/UnitPriceAtOrderTime`; `OrderStatus` enum (`Pending, Paid, Shipped, Cancelled`); `Customer.Id/Name/Email`; `AdminUser.Id/Username/PasswordHash`. These exact names are consumed by Application (Tasks 3-5).

- [ ] **Step 1: Write the failing domain tests**

`tests/Domain.UnitTests/ProductTests.cs`:

```csharp
using Domain;
using Domain.Exceptions;
using Xunit;

public class ProductTests
{
    [Fact]
    public void DecreaseStock_reduces_quantity_when_enough_stock()
    {
        var product = new Product(Guid.NewGuid(), "Widget", 10.00m, stockQuantity: 5);

        product.DecreaseStock(3);

        Assert.Equal(2, product.StockQuantity);
    }

    [Fact]
    public void DecreaseStock_throws_when_insufficient_stock()
    {
        var product = new Product(Guid.NewGuid(), "Widget", 10.00m, stockQuantity: 2);

        Assert.Throws<InsufficientStockException>(() => product.DecreaseStock(3));
    }

    [Fact]
    public void IncreaseStock_adds_quantity_back()
    {
        var product = new Product(Guid.NewGuid(), "Widget", 10.00m, stockQuantity: 2);

        product.IncreaseStock(5);

        Assert.Equal(7, product.StockQuantity);
    }
}
```

`tests/Domain.UnitTests/OrderTests.cs`:

```csharp
using Domain;
using Domain.Exceptions;
using Xunit;

public class OrderTests
{
    private static OrderItem Item(int quantity = 1) =>
        new(Guid.NewGuid(), quantity, unitPriceAtOrderTime: 10.00m);

    [Fact]
    public void Create_starts_in_Pending_status()
    {
        var order = Order.Create(Guid.NewGuid(), new[] { Item() });

        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Paid)]
    [InlineData(OrderStatus.Paid, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled)]
    public void AdvanceTo_allows_valid_transitions(OrderStatus from, OrderStatus to)
    {
        var order = Order.Create(Guid.NewGuid(), new[] { Item() });
        if (from != OrderStatus.Pending)
        {
            order.AdvanceTo(from);
        }

        order.AdvanceTo(to);

        Assert.Equal(to, order.Status);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Pending)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid)]
    public void AdvanceTo_rejects_invalid_transitions(OrderStatus from, OrderStatus to)
    {
        var order = Order.Create(Guid.NewGuid(), new[] { Item() });
        if (from != OrderStatus.Pending)
        {
            order.AdvanceTo(from);
        }

        Assert.Throws<InvalidOrderStatusTransitionException>(() => order.AdvanceTo(to));
    }

    [Fact]
    public void Cancel_from_Pending_marks_order_Cancelled()
    {
        var order = Order.Create(Guid.NewGuid(), new[] { Item() });

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_from_Shipped_throws()
    {
        var order = Order.Create(Guid.NewGuid(), new[] { Item() });
        order.AdvanceTo(OrderStatus.Paid);
        order.AdvanceTo(OrderStatus.Shipped);

        Assert.Throws<InvalidOrderStatusTransitionException>(() => order.Cancel());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile (types don't exist yet)**

Run: `dotnet test tests/Domain.UnitTests`
Expected: Build errors — `Product`, `Order`, `OrderItem`, `OrderStatus`, `InsufficientStockException`, `InvalidOrderStatusTransitionException` do not exist.

- [ ] **Step 3: Implement the exception hierarchy**

`src/Domain/Exceptions/DomainException.cs`:

```csharp
namespace Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
```

`src/Domain/Exceptions/InsufficientStockException.cs`:

```csharp
namespace Domain.Exceptions;

public class InsufficientStockException : DomainException
{
    public InsufficientStockException(Guid productId, int requested, int available)
        : base($"Product {productId} has {available} in stock, but {requested} were requested.") { }
}
```

`src/Domain/Exceptions/InvalidOrderStatusTransitionException.cs`:

```csharp
namespace Domain.Exceptions;

public class InvalidOrderStatusTransitionException : DomainException
{
    public InvalidOrderStatusTransitionException(OrderStatus from, OrderStatus to)
        : base($"Cannot transition an order from {from} to {to}.") { }
}
```

- [ ] **Step 4: Implement `Product`**

`src/Domain/Product.cs`:

```csharp
using Domain.Exceptions;

namespace Domain;

public class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public int StockQuantity { get; private set; }

    public Product(Guid id, string name, decimal price, int stockQuantity)
    {
        Id = id;
        Name = name;
        Price = price;
        StockQuantity = stockQuantity;
    }

    private Product() { Name = string.Empty; }

    public void DecreaseStock(int quantity)
    {
        if (quantity > StockQuantity)
        {
            throw new InsufficientStockException(Id, quantity, StockQuantity);
        }

        StockQuantity -= quantity;
    }

    public void IncreaseStock(int quantity)
    {
        StockQuantity += quantity;
    }

    public void UpdateDetails(string name, decimal price)
    {
        Name = name;
        Price = price;
    }
}
```

- [ ] **Step 5: Implement `OrderStatus`, `OrderItem`, `Order`, `Customer`, `AdminUser`**

`src/Domain/OrderStatus.cs`:

```csharp
namespace Domain;

public enum OrderStatus
{
    Pending,
    Paid,
    Shipped,
    Cancelled
}
```

`src/Domain/OrderItem.cs`:

```csharp
namespace Domain;

public class OrderItem
{
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPriceAtOrderTime { get; private set; }

    public OrderItem(Guid productId, int quantity, decimal unitPriceAtOrderTime)
    {
        ProductId = productId;
        Quantity = quantity;
        UnitPriceAtOrderTime = unitPriceAtOrderTime;
    }

    private OrderItem() { }
}
```

`src/Domain/Order.cs`:

```csharp
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

        Status = newStatus;
    }

    public void Cancel()
    {
        AdvanceTo(OrderStatus.Cancelled);
    }
}
```

`src/Domain/Customer.cs`:

```csharp
namespace Domain;

public class Customer
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }

    public Customer(Guid id, string name, string email)
    {
        Id = id;
        Name = name;
        Email = email;
    }

    private Customer() { Name = string.Empty; Email = string.Empty; }
}
```

`src/Domain/AdminUser.cs`:

```csharp
namespace Domain;

public class AdminUser
{
    public Guid Id { get; private set; }
    public string Username { get; private set; }
    public string PasswordHash { get; private set; }

    public AdminUser(Guid id, string username, string passwordHash)
    {
        Id = id;
        Username = username;
        PasswordHash = passwordHash;
    }

    private AdminUser() { Username = string.Empty; PasswordHash = string.Empty; }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Domain.UnitTests`
Expected: All tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Domain tests/Domain.UnitTests
git commit -m "feat(domain): add entities, order status transitions, and stock rules"
```

---

### Task 3: Application Layer — Interfaces and DTOs

**Files:**
- Create: `src/Application/Repositories/IProductRepository.cs`
- Create: `src/Application/Repositories/IOrderRepository.cs`
- Create: `src/Application/Repositories/IAdminUserRepository.cs`
- Create: `src/Application/Dtos/ProductDto.cs`
- Create: `src/Application/Dtos/OrderDto.cs`
- Create: `src/Application/Dtos/OrderItemDto.cs`

**Interfaces:**
- Consumes: `Domain.Product`, `Domain.Order`, `Domain.AdminUser` (Task 2).
- Produces: `IProductRepository` (`GetAllAsync`, `GetByIdAsync(Guid)`, `AddAsync(Product)`, `UpdateAsync(Product)`, `DeleteAsync(Guid)`, `SaveChangesAsync`), `IOrderRepository` (`GetAllAsync(OrderStatus? filter)`, `GetByIdAsync(Guid)`, `AddAsync(Order)`, `SaveChangesAsync`), `IAdminUserRepository` (`GetByUsernameAsync(string)`), `ProductDto(Guid Id, string Name, decimal Price, int StockQuantity)`, `OrderItemDto(Guid ProductId, int Quantity, decimal UnitPriceAtOrderTime)`, `OrderDto(Guid Id, Guid CustomerId, string Status, IReadOnlyList<OrderItemDto> Items)`. Consumed by Tasks 4, 5, 7.

This task has no business logic to test — it's pure contracts. No test step; the contracts are exercised by the tests in Tasks 4 and 5.

- [ ] **Step 1: Add DTOs**

`src/Application/Dtos/ProductDto.cs`:

```csharp
namespace Application.Dtos;

public record ProductDto(Guid Id, string Name, decimal Price, int StockQuantity);
```

`src/Application/Dtos/OrderItemDto.cs`:

```csharp
namespace Application.Dtos;

public record OrderItemDto(Guid ProductId, int Quantity, decimal UnitPriceAtOrderTime);
```

`src/Application/Dtos/OrderDto.cs`:

```csharp
namespace Application.Dtos;

public record OrderDto(Guid Id, Guid CustomerId, string Status, IReadOnlyList<OrderItemDto> Items);
```

- [ ] **Step 2: Add repository interfaces**

`src/Application/Repositories/IProductRepository.cs`:

```csharp
using Domain;

namespace Application.Repositories;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(Guid id);
    Task AddAsync(Product product);
    Task DeleteAsync(Guid id);
    Task SaveChangesAsync();
}
```

`src/Application/Repositories/IOrderRepository.cs`:

```csharp
using Domain;

namespace Application.Repositories;

public interface IOrderRepository
{
    Task<IReadOnlyList<Order>> GetAllAsync(OrderStatus? statusFilter);
    Task<Order?> GetByIdAsync(Guid id);
    Task AddAsync(Order order);
    Task SaveChangesAsync();
}
```

`src/Application/Repositories/IAdminUserRepository.cs`:

```csharp
using Domain;

namespace Application.Repositories;

public interface IAdminUserRepository
{
    Task<AdminUser?> GetByUsernameAsync(string username);
}
```

- [ ] **Step 3: Verify the solution builds**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Application/Repositories src/Application/Dtos
git commit -m "feat(application): add repository interfaces and DTOs"
```

---

### Task 4: Application — CreateOrderHandler

**Files:**
- Create: `src/Application/Orders/CreateOrderRequest.cs`
- Create: `src/Application/Orders/CreateOrderHandler.cs`
- Test: `tests/Application.UnitTests/Fakes/FakeProductRepository.cs`
- Test: `tests/Application.UnitTests/Fakes/FakeOrderRepository.cs`
- Test: `tests/Application.UnitTests/CreateOrderHandlerTests.cs`

**Interfaces:**
- Consumes: `IProductRepository`, `IOrderRepository` (Task 3); `Product`, `Order`, `OrderItem` (Task 2).
- Produces: `CreateOrderRequest(Guid CustomerId, IReadOnlyList<CreateOrderLineRequest> Lines)` where `CreateOrderLineRequest(Guid ProductId, int Quantity)`; `CreateOrderHandler(IProductRepository, IOrderRepository)` with `Task<OrderDto> HandleAsync(CreateOrderRequest request)`, throwing `InsufficientStockException` (via `Product.DecreaseStock`) when any line lacks stock, with no partial writes. Consumed by the `Api` layer in Task 9.

- [ ] **Step 1: Write the fakes and the failing test**

`tests/Application.UnitTests/Fakes/FakeProductRepository.cs`:

```csharp
using Application.Repositories;
using Domain;

namespace Application.UnitTests.Fakes;

public class FakeProductRepository : IProductRepository
{
    private readonly Dictionary<Guid, Product> _products = new();

    public void Seed(Product product) => _products[product.Id] = product;

    public Task<IReadOnlyList<Product>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Product>>(_products.Values.ToList());

    public Task<Product?> GetByIdAsync(Guid id) =>
        Task.FromResult(_products.GetValueOrDefault(id));

    public Task AddAsync(Product product)
    {
        _products[product.Id] = product;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _products.Remove(id);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => Task.CompletedTask;
}
```

`tests/Application.UnitTests/Fakes/FakeOrderRepository.cs`:

```csharp
using Application.Repositories;
using Domain;

namespace Application.UnitTests.Fakes;

public class FakeOrderRepository : IOrderRepository
{
    public readonly List<Order> Orders = new();

    public Task<IReadOnlyList<Order>> GetAllAsync(OrderStatus? statusFilter) =>
        Task.FromResult<IReadOnlyList<Order>>(
            Orders.Where(o => statusFilter == null || o.Status == statusFilter).ToList());

    public Task<Order?> GetByIdAsync(Guid id) =>
        Task.FromResult(Orders.FirstOrDefault(o => o.Id == id));

    public Task AddAsync(Order order)
    {
        Orders.Add(order);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => Task.CompletedTask;
}
```

`tests/Application.UnitTests/CreateOrderHandlerTests.cs`:

```csharp
using Application.Orders;
using Application.UnitTests.Fakes;
using Domain;
using Domain.Exceptions;
using Xunit;

namespace Application.UnitTests;

public class CreateOrderHandlerTests
{
    [Fact]
    public async Task HandleAsync_creates_order_and_decrements_stock()
    {
        var productId = Guid.NewGuid();
        var products = new FakeProductRepository();
        products.Seed(new Product(productId, "Widget", 10.00m, stockQuantity: 5));
        var orders = new FakeOrderRepository();
        var handler = new CreateOrderHandler(products, orders);

        var result = await handler.HandleAsync(new CreateOrderRequest(
            Guid.NewGuid(),
            new[] { new CreateOrderLineRequest(productId, 3) }));

        Assert.Equal("Pending", result.Status);
        Assert.Equal(2, (await products.GetByIdAsync(productId))!.StockQuantity);
        Assert.Single(orders.Orders);
    }

    [Fact]
    public async Task HandleAsync_rejects_whole_order_when_any_line_lacks_stock()
    {
        var plentyId = Guid.NewGuid();
        var scarceId = Guid.NewGuid();
        var products = new FakeProductRepository();
        products.Seed(new Product(plentyId, "Plenty", 5.00m, stockQuantity: 10));
        products.Seed(new Product(scarceId, "Scarce", 5.00m, stockQuantity: 1));
        var orders = new FakeOrderRepository();
        var handler = new CreateOrderHandler(products, orders);

        await Assert.ThrowsAsync<InsufficientStockException>(() => handler.HandleAsync(
            new CreateOrderRequest(Guid.NewGuid(), new[]
            {
                new CreateOrderLineRequest(plentyId, 2),
                new CreateOrderLineRequest(scarceId, 5),
            })));

        Assert.Equal(10, (await products.GetByIdAsync(plentyId))!.StockQuantity);
        Assert.Equal(1, (await products.GetByIdAsync(scarceId))!.StockQuantity);
        Assert.Empty(orders.Orders);
    }
}
```

- [ ] **Step 2: Run to verify it fails to compile**

Run: `dotnet test tests/Application.UnitTests`
Expected: Build errors — `CreateOrderHandler`, `CreateOrderRequest`, `CreateOrderLineRequest` do not exist.

- [ ] **Step 3: Implement the request types and handler**

`src/Application/Orders/CreateOrderRequest.cs`:

```csharp
namespace Application.Orders;

public record CreateOrderLineRequest(Guid ProductId, int Quantity);

public record CreateOrderRequest(Guid CustomerId, IReadOnlyList<CreateOrderLineRequest> Lines);
```

`src/Application/Orders/CreateOrderHandler.cs`:

```csharp
using Application.Dtos;
using Application.Repositories;
using Domain;
using Domain.Exceptions;

namespace Application.Orders;

public class CreateOrderHandler
{
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;

    public CreateOrderHandler(IProductRepository products, IOrderRepository orders)
    {
        _products = products;
        _orders = orders;
    }

    public async Task<OrderDto> HandleAsync(CreateOrderRequest request)
    {
        var resolvedProducts = new List<(Product Product, int Quantity)>();

        foreach (var line in request.Lines)
        {
            var product = await _products.GetByIdAsync(line.ProductId)
                ?? throw new InsufficientStockException(line.ProductId, line.Quantity, 0);
            if (line.Quantity > product.StockQuantity)
            {
                throw new InsufficientStockException(line.ProductId, line.Quantity, product.StockQuantity);
            }
            resolvedProducts.Add((product, line.Quantity));
        }

        var orderItems = resolvedProducts
            .Select(rp => new OrderItem(rp.Product.Id, rp.Quantity, rp.Product.Price))
            .ToList();
        var order = Order.Create(request.CustomerId, orderItems);

        foreach (var (product, quantity) in resolvedProducts)
        {
            product.DecreaseStock(quantity);
        }

        await _orders.AddAsync(order);
        await _orders.SaveChangesAsync();
        await _products.SaveChangesAsync();

        return new OrderDto(
            order.Id,
            order.CustomerId,
            order.Status.ToString(),
            order.Items.Select(i => new OrderItemDto(i.ProductId, i.Quantity, i.UnitPriceAtOrderTime)).ToList());
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Application.UnitTests`
Expected: All tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Application/Orders tests/Application.UnitTests
git commit -m "feat(application): add CreateOrderHandler with all-or-nothing stock validation"
```

---

### Task 5: Application — Remaining Handlers

**Files:**
- Create: `src/Application/Products/ProductHandlers.cs`
- Create: `src/Application/Orders/OrderQueryHandlers.cs`
- Create: `src/Application/Orders/UpdateOrderStatusHandler.cs`
- Create: `src/Application/Auth/LoginHandler.cs`
- Create: `src/Application/Auth/IPasswordHasher.cs`
- Create: `src/Application/Auth/ITokenGenerator.cs`
- Test: `tests/Application.UnitTests/Fakes/FakeAdminUserRepository.cs`
- Test: `tests/Application.UnitTests/ProductHandlersTests.cs`
- Test: `tests/Application.UnitTests/UpdateOrderStatusHandlerTests.cs`
- Test: `tests/Application.UnitTests/LoginHandlerTests.cs`

**Interfaces:**
- Consumes: `IProductRepository`, `IOrderRepository`, `IAdminUserRepository` (Task 3); `Product`, `Order`, `AdminUser` (Task 2); `ProductDto`, `OrderDto` (Task 3).
- Produces: `GetProductsHandler.HandleAsync()`, `CreateProductHandler.HandleAsync(CreateProductRequest)`, `UpdateProductHandler.HandleAsync(Guid, UpdateProductRequest)`, `DeleteProductHandler.HandleAsync(Guid)`; `GetOrdersHandler.HandleAsync(OrderStatus?)`, `GetOrderByIdHandler.HandleAsync(Guid)`; `UpdateOrderStatusHandler.HandleAsync(Guid, OrderStatus)` (returns stock to `Product` on cancellation); `IPasswordHasher.Hash(string)`/`Verify(string, string)`; `ITokenGenerator.GenerateToken(AdminUser)`; `LoginHandler.HandleAsync(string username, string password)` returning `string?` token (`null` on bad credentials). All consumed by `Api` controllers in Task 9; `IPasswordHasher`/`ITokenGenerator` implemented by `Infrastructure` in Task 8.

- [ ] **Step 1: Write the fake and the failing tests**

`tests/Application.UnitTests/Fakes/FakeAdminUserRepository.cs`:

```csharp
using Application.Repositories;
using Domain;

namespace Application.UnitTests.Fakes;

public class FakeAdminUserRepository : IAdminUserRepository
{
    private readonly Dictionary<string, AdminUser> _users = new();

    public void Seed(AdminUser user) => _users[user.Username] = user;

    public Task<AdminUser?> GetByUsernameAsync(string username) =>
        Task.FromResult(_users.GetValueOrDefault(username));
}
```

`tests/Application.UnitTests/ProductHandlersTests.cs`:

```csharp
using Application.Products;
using Application.UnitTests.Fakes;
using Domain;
using Xunit;

namespace Application.UnitTests;

public class ProductHandlersTests
{
    [Fact]
    public async Task CreateProductHandler_adds_and_returns_product()
    {
        var repo = new FakeProductRepository();
        var handler = new CreateProductHandler(repo);

        var result = await handler.HandleAsync(new CreateProductRequest("Widget", 9.99m, 100));

        Assert.Equal("Widget", result.Name);
        Assert.Single(await repo.GetAllAsync());
    }

    [Fact]
    public async Task DeleteProductHandler_removes_product()
    {
        var id = Guid.NewGuid();
        var repo = new FakeProductRepository();
        repo.Seed(new Product(id, "Widget", 9.99m, 100));
        var handler = new DeleteProductHandler(repo);

        await handler.HandleAsync(id);

        Assert.Empty(await repo.GetAllAsync());
    }
}
```

`tests/Application.UnitTests/UpdateOrderStatusHandlerTests.cs`:

```csharp
using Application.Orders;
using Application.UnitTests.Fakes;
using Domain;
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
        var handler = new UpdateOrderStatusHandler(orders, products);

        await handler.HandleAsync(order.Id, OrderStatus.Cancelled);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(8, (await products.GetByIdAsync(productId))!.StockQuantity);
    }
}
```

`tests/Application.UnitTests/LoginHandlerTests.cs`:

```csharp
using Application.Auth;
using Application.UnitTests.Fakes;
using Domain;
using Xunit;

namespace Application.UnitTests;

public class LoginHandlerTests
{
    private class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";
        public bool Verify(string password, string hash) => hash == $"hashed:{password}";
    }

    private class FakeTokenGenerator : ITokenGenerator
    {
        public string GenerateToken(AdminUser user) => $"token-for-{user.Username}";
    }

    [Fact]
    public async Task HandleAsync_returns_token_for_valid_credentials()
    {
        var users = new FakeAdminUserRepository();
        users.Seed(new AdminUser(Guid.NewGuid(), "admin", "hashed:secret"));
        var handler = new LoginHandler(users, new FakeHasher(), new FakeTokenGenerator());

        var token = await handler.HandleAsync("admin", "secret");

        Assert.Equal("token-for-admin", token);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_wrong_password()
    {
        var users = new FakeAdminUserRepository();
        users.Seed(new AdminUser(Guid.NewGuid(), "admin", "hashed:secret"));
        var handler = new LoginHandler(users, new FakeHasher(), new FakeTokenGenerator());

        var token = await handler.HandleAsync("admin", "wrong");

        Assert.Null(token);
    }
}
```

- [ ] **Step 2: Run to verify it fails to compile**

Run: `dotnet test tests/Application.UnitTests`
Expected: Build errors — the handler/interface types below do not exist yet.

- [ ] **Step 3: Implement product handlers**

`src/Application/Products/ProductHandlers.cs`:

```csharp
using Application.Dtos;
using Application.Repositories;
using Domain;

namespace Application.Products;

public record CreateProductRequest(string Name, decimal Price, int StockQuantity);
public record UpdateProductRequest(string Name, decimal Price);

public class GetProductsHandler
{
    private readonly IProductRepository _products;
    public GetProductsHandler(IProductRepository products) => _products = products;

    public async Task<IReadOnlyList<ProductDto>> HandleAsync()
    {
        var products = await _products.GetAllAsync();
        return products.Select(ToDto).ToList();
    }

    internal static ProductDto ToDto(Product p) => new(p.Id, p.Name, p.Price, p.StockQuantity);
}

public class CreateProductHandler
{
    private readonly IProductRepository _products;
    public CreateProductHandler(IProductRepository products) => _products = products;

    public async Task<ProductDto> HandleAsync(CreateProductRequest request)
    {
        var product = new Product(Guid.NewGuid(), request.Name, request.Price, request.StockQuantity);
        await _products.AddAsync(product);
        await _products.SaveChangesAsync();
        return GetProductsHandler.ToDto(product);
    }
}

public class UpdateProductHandler
{
    private readonly IProductRepository _products;
    public UpdateProductHandler(IProductRepository products) => _products = products;

    public async Task<ProductDto?> HandleAsync(Guid id, UpdateProductRequest request)
    {
        var product = await _products.GetByIdAsync(id);
        if (product is null) return null;

        product.UpdateDetails(request.Name, request.Price);
        await _products.SaveChangesAsync();
        return GetProductsHandler.ToDto(product);
    }
}

public class DeleteProductHandler
{
    private readonly IProductRepository _products;
    public DeleteProductHandler(IProductRepository products) => _products = products;

    public async Task HandleAsync(Guid id)
    {
        await _products.DeleteAsync(id);
        await _products.SaveChangesAsync();
    }
}
```

- [ ] **Step 4: Implement order query and status handlers**

`src/Application/Orders/OrderQueryHandlers.cs`:

```csharp
using Application.Dtos;
using Application.Repositories;
using Domain;

namespace Application.Orders;

public class GetOrdersHandler
{
    private readonly IOrderRepository _orders;
    public GetOrdersHandler(IOrderRepository orders) => _orders = orders;

    public async Task<IReadOnlyList<OrderDto>> HandleAsync(OrderStatus? statusFilter)
    {
        var orders = await _orders.GetAllAsync(statusFilter);
        return orders.Select(ToDto).ToList();
    }

    internal static OrderDto ToDto(Order o) => new(
        o.Id,
        o.CustomerId,
        o.Status.ToString(),
        o.Items.Select(i => new OrderItemDto(i.ProductId, i.Quantity, i.UnitPriceAtOrderTime)).ToList());
}

public class GetOrderByIdHandler
{
    private readonly IOrderRepository _orders;
    public GetOrderByIdHandler(IOrderRepository orders) => _orders = orders;

    public async Task<OrderDto?> HandleAsync(Guid id)
    {
        var order = await _orders.GetByIdAsync(id);
        return order is null ? null : GetOrdersHandler.ToDto(order);
    }
}
```

`src/Application/Orders/UpdateOrderStatusHandler.cs`:

```csharp
using Application.Dtos;
using Application.Repositories;
using Domain;

namespace Application.Orders;

public class UpdateOrderStatusHandler
{
    private readonly IOrderRepository _orders;
    private readonly IProductRepository _products;

    public UpdateOrderStatusHandler(IOrderRepository orders, IProductRepository products)
    {
        _orders = orders;
        _products = products;
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
        return GetOrdersHandler.ToDto(order);
    }
}
```

- [ ] **Step 5: Implement auth contracts and `LoginHandler`**

`src/Application/Auth/IPasswordHasher.cs`:

```csharp
namespace Application.Auth;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
```

`src/Application/Auth/ITokenGenerator.cs`:

```csharp
using Domain;

namespace Application.Auth;

public interface ITokenGenerator
{
    string GenerateToken(AdminUser user);
}
```

`src/Application/Auth/LoginHandler.cs`:

```csharp
using Application.Repositories;

namespace Application.Auth;

public class LoginHandler
{
    private readonly IAdminUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenGenerator _tokenGenerator;

    public LoginHandler(IAdminUserRepository users, IPasswordHasher hasher, ITokenGenerator tokenGenerator)
    {
        _users = users;
        _hasher = hasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<string?> HandleAsync(string username, string password)
    {
        var user = await _users.GetByUsernameAsync(username);
        if (user is null || !_hasher.Verify(password, user.PasswordHash))
        {
            return null;
        }

        return _tokenGenerator.GenerateToken(user);
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Application.UnitTests`
Expected: All tests PASS (Task 4's tests still pass too).

- [ ] **Step 7: Commit**

```bash
git add src/Application/Products src/Application/Orders src/Application/Auth tests/Application.UnitTests
git commit -m "feat(application): add product, order-status, and login handlers"
```

---

### Task 6: Infrastructure — EF Core DbContext and Configurations

**Files:**
- Create: `src/Infrastructure/Infrastructure.csproj` (add packages)
- Create: `src/Infrastructure/Persistence/AppDbContext.cs`
- Create: `src/Infrastructure/Persistence/Configurations/ProductConfiguration.cs`
- Create: `src/Infrastructure/Persistence/Configurations/OrderConfiguration.cs`
- Create: `src/Infrastructure/Persistence/Configurations/CustomerConfiguration.cs`
- Create: `src/Infrastructure/Persistence/Configurations/AdminUserConfiguration.cs`

**Interfaces:**
- Consumes: `Product`, `Order`, `OrderItem`, `Customer`, `AdminUser` (Task 2).
- Produces: `AppDbContext` with `DbSet<Product> Products`, `DbSet<Order> Orders`, `DbSet<Customer> Customers`, `DbSet<AdminUser> AdminUsers`. Consumed by repository implementations (Task 7) and integration tests (Task 10).

- [ ] **Step 1: Add EF Core packages**

```bash
dotnet add src/Infrastructure/Infrastructure.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.0
dotnet add src/Infrastructure/Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design --version 8.0.0
dotnet tool install --global dotnet-ef --version 8.0.0
```

- [ ] **Step 2: Write `AppDbContext`**

`src/Infrastructure/Persistence/AppDbContext.cs`:

```csharp
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

- [ ] **Step 3: Write entity configurations**

`src/Infrastructure/Persistence/Configurations/ProductConfiguration.cs`:

```csharp
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Price).HasColumnType("decimal(18,2)");
    }
}
```

`src/Infrastructure/Persistence/Configurations/OrderConfiguration.cs`:

```csharp
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);

        builder.OwnsMany(o => o.Items, items =>
        {
            items.WithOwner().HasForeignKey("OrderId");
            items.Property<int>("Id");
            items.HasKey("Id");
            items.Property(i => i.UnitPriceAtOrderTime).HasColumnType("decimal(18,2)");
            items.ToTable("OrderItems");
        });

        builder.Navigation(o => o.Items).UsePropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);
    }
}
```

`src/Infrastructure/Persistence/Configurations/CustomerConfiguration.cs`:

```csharp
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(200);
    }
}
```

`src/Infrastructure/Persistence/Configurations/AdminUserConfiguration.cs`:

```csharp
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).IsRequired().HasMaxLength(100);
        builder.HasIndex(u => u.Username).IsUnique();
        builder.Property(u => u.PasswordHash).IsRequired();
    }
}
```

Note: `Order.Items` and `OrderItem`'s constructor are private-setter with no parameterless public constructor accessible outside `Domain` — EF Core uses the private parameterless constructors already present on `Order` and `OrderItem` (Task 2) via backing fields, which is why `UsePropertyAccessMode(Field)` is set above.

- [ ] **Step 4: Verify the solution builds**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Infrastructure
git commit -m "feat(infrastructure): add EF Core DbContext and entity configurations"
```

---

### Task 7: Infrastructure — Repository Implementations

**Files:**
- Create: `src/Infrastructure/Persistence/ProductRepository.cs`
- Create: `src/Infrastructure/Persistence/OrderRepository.cs`
- Create: `src/Infrastructure/Persistence/AdminUserRepository.cs`

**Interfaces:**
- Consumes: `AppDbContext` (Task 6); `IProductRepository`, `IOrderRepository`, `IAdminUserRepository` (Task 3).
- Produces: EF-Core-backed implementations of all three repository interfaces. Consumed by DI wiring in Task 9.

- [ ] **Step 1: Implement `ProductRepository`**

`src/Infrastructure/Persistence/ProductRepository.cs`:

```csharp
using Application.Repositories;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;
    public ProductRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Product>> GetAllAsync() =>
        await _db.Products.ToListAsync();

    public Task<Product?> GetByIdAsync(Guid id) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == id);

    public async Task AddAsync(Product product) =>
        await _db.Products.AddAsync(product);

    public async Task DeleteAsync(Guid id)
    {
        var product = await GetByIdAsync(id);
        if (product is not null)
        {
            _db.Products.Remove(product);
        }
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
```

- [ ] **Step 2: Implement `OrderRepository`**

`src/Infrastructure/Persistence/OrderRepository.cs`:

```csharp
using Application.Repositories;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;
    public OrderRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Order>> GetAllAsync(OrderStatus? statusFilter)
    {
        var query = _db.Orders.AsQueryable();
        if (statusFilter is not null)
        {
            query = query.Where(o => o.Status == statusFilter);
        }
        return await query.ToListAsync();
    }

    public Task<Order?> GetByIdAsync(Guid id) =>
        _db.Orders.FirstOrDefaultAsync(o => o.Id == id);

    public async Task AddAsync(Order order) =>
        await _db.Orders.AddAsync(order);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
```

- [ ] **Step 3: Implement `AdminUserRepository`**

`src/Infrastructure/Persistence/AdminUserRepository.cs`:

```csharp
using Application.Repositories;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AdminUserRepository : IAdminUserRepository
{
    private readonly AppDbContext _db;
    public AdminUserRepository(AppDbContext db) => _db = db;

    public Task<AdminUser?> GetByUsernameAsync(string username) =>
        _db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username);
}
```

- [ ] **Step 4: Verify the solution builds**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Infrastructure/Persistence
git commit -m "feat(infrastructure): implement EF Core repositories"
```

---

### Task 8: Infrastructure — JWT Token Generation and Password Hashing

**Files:**
- Create: `src/Infrastructure/Auth/JwtSettings.cs`
- Create: `src/Infrastructure/Auth/TokenGenerator.cs`
- Create: `src/Infrastructure/Auth/PasswordHasher.cs`
- Test: `tests/Application.UnitTests` is not touched — these are Infrastructure concerns exercised end-to-end by Task 10's integration tests.

**Interfaces:**
- Consumes: `ITokenGenerator`, `IPasswordHasher` (Task 5); `AdminUser` (Task 2).
- Produces: `JwtSettings(string Issuer, string Audience, string SigningKey, int ExpiryMinutes)`; `TokenGenerator : ITokenGenerator`; `PasswordHasher : IPasswordHasher` (using `System.Security.Cryptography` PBKDF2, no external hashing package). Consumed by DI wiring and JWT Bearer configuration in Task 9.

- [ ] **Step 1: Add the JWT package**

```bash
dotnet add src/Infrastructure/Infrastructure.csproj package Microsoft.IdentityModel.Tokens --version 8.0.1
dotnet add src/Infrastructure/Infrastructure.csproj package System.IdentityModel.Tokens.Jwt --version 8.0.1
```

- [ ] **Step 2: Write `JwtSettings`**

`src/Infrastructure/Auth/JwtSettings.cs`:

```csharp
namespace Infrastructure.Auth;

public class JwtSettings
{
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string SigningKey { get; set; }
    public int ExpiryMinutes { get; set; } = 60;
}
```

- [ ] **Step 3: Write `TokenGenerator`**

`src/Infrastructure/Auth/TokenGenerator.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Auth;
using Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Auth;

public class TokenGenerator : ITokenGenerator
{
    private readonly JwtSettings _settings;
    public TokenGenerator(IOptions<JwtSettings> settings) => _settings = settings.Value;

    public string GenerateToken(AdminUser user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Username),
            new Claim("adminUserId", user.Id.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Step 4: Write `PasswordHasher`**

`src/Infrastructure/Auth/PasswordHasher.cs`:

```csharp
using System.Security.Cryptography;
using Application.Auth;

namespace Infrastructure.Auth;

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
    }
}
```

- [ ] **Step 5: Verify the solution builds**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/Infrastructure/Auth
git commit -m "feat(infrastructure): add JWT token generation and PBKDF2 password hashing"
```

---

### Task 9: Api — Controllers, DI Wiring, and Error Middleware

**Files:**
- Modify: `src/Api/Api.csproj` (add packages)
- Create: `src/Api/Middleware/DomainExceptionMiddleware.cs`
- Create: `src/Api/Controllers/ProductsController.cs`
- Create: `src/Api/Controllers/OrdersController.cs`
- Create: `src/Api/Controllers/AuthController.cs`
- Create: `src/Api/Contracts/LoginRequest.cs`
- Create: `src/Api/Contracts/CreateOrderApiRequest.cs`
- Create: `src/Api/Contracts/UpdateOrderStatusApiRequest.cs`
- Modify: `src/Api/Program.cs`
- Modify: `src/Api/appsettings.json`

**Interfaces:**
- Consumes: every `Application` handler (Tasks 4-5), every `Infrastructure` implementation (Tasks 6-8), `AppDbContext` (Task 6).
- Produces: the running HTTP API — `GET/POST /api/products`, `PUT/DELETE /api/products/{id}`, `GET /api/orders`, `GET /api/orders/{id}`, `POST /api/orders`, `PATCH /api/orders/{id}/status`, `POST /api/auth/login`. Consumed by Task 10's integration tests and both frontends (Tasks 12-13).

- [ ] **Step 1: Add packages and reference EF Core in `Api` for `Program.cs` wiring**

```bash
dotnet add src/Api/Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.0
dotnet add src/Api/Api.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.0
```

- [ ] **Step 2: Write the central error-handling middleware**

`src/Api/Middleware/DomainExceptionMiddleware.cs`:

```csharp
using Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Middleware;

public class DomainExceptionMiddleware
{
    private readonly RequestDelegate _next;
    public DomainExceptionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Business rule violated",
                Detail = ex.Message,
            };
            await context.Response.WriteAsJsonAsync(problem);
        }
        catch (KeyNotFoundException ex)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = ex.Message,
            };
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
```

- [ ] **Step 3: Write request contracts**

`src/Api/Contracts/LoginRequest.cs`:

```csharp
namespace Api.Contracts;

public record LoginRequest(string Username, string Password);
```

`src/Api/Contracts/CreateOrderApiRequest.cs`:

```csharp
namespace Api.Contracts;

public record CreateOrderLineApiRequest(Guid ProductId, int Quantity);
public record CreateOrderApiRequest(Guid CustomerId, IReadOnlyList<CreateOrderLineApiRequest> Lines);
```

`src/Api/Contracts/UpdateOrderStatusApiRequest.cs`:

```csharp
namespace Api.Contracts;

public record UpdateOrderStatusApiRequest(string Status);
```

- [ ] **Step 4: Write `ProductsController`**

`src/Api/Controllers/ProductsController.cs`:

```csharp
using Application.Dtos;
using Application.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetAll(
        [FromServices] GetProductsHandler handler) =>
        Ok(await handler.HandleAsync());

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ProductDto>> Create(
        [FromServices] CreateProductHandler handler,
        [FromBody] CreateProductRequest request) =>
        Ok(await handler.HandleAsync(request));

    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<ProductDto>> Update(
        Guid id,
        [FromServices] UpdateProductHandler handler,
        [FromBody] UpdateProductRequest request)
    {
        var result = await handler.HandleAsync(id, request);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeleteProductHandler handler)
    {
        await handler.HandleAsync(id);
        return NoContent();
    }
}
```

- [ ] **Step 5: Write `OrdersController`**

`src/Api/Controllers/OrdersController.cs`:

```csharp
using Api.Contracts;
using Application.Dtos;
using Application.Orders;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetAll(
        [FromServices] GetOrdersHandler handler,
        [FromQuery] string? status)
    {
        OrderStatus? filter = status is null ? null : Enum.Parse<OrderStatus>(status, ignoreCase: true);
        return Ok(await handler.HandleAsync(filter));
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<OrderDto>> Create(
        [FromServices] CreateOrderHandler handler,
        [FromBody] CreateOrderApiRequest request)
    {
        var result = await handler.HandleAsync(new CreateOrderRequest(
            request.CustomerId,
            request.Lines.Select(l => new CreateOrderLineRequest(l.ProductId, l.Quantity)).ToList()));
        return Ok(result);
    }

    [HttpPatch("{id}/status")]
    [Authorize]
    public async Task<ActionResult<OrderDto>> UpdateStatus(
        Guid id,
        [FromServices] UpdateOrderStatusHandler handler,
        [FromBody] UpdateOrderStatusApiRequest request)
    {
        var newStatus = Enum.Parse<OrderStatus>(request.Status, ignoreCase: true);
        return Ok(await handler.HandleAsync(id, newStatus));
    }
}
```

- [ ] **Step 6: Write `AuthController`**

`src/Api/Controllers/AuthController.cs`:

```csharp
using Api.Contracts;
using Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromServices] LoginHandler handler,
        [FromBody] LoginRequest request)
    {
        var token = await handler.HandleAsync(request.Username, request.Password);
        return token is null ? Unauthorized() : Ok(new { token });
    }
}
```

- [ ] **Step 7: Wire DI, JWT auth, and the middleware in `Program.cs`**

`src/Api/Program.cs`:

```csharp
using System.Text;
using Application.Auth;
using Application.Orders;
using Application.Products;
using Application.Repositories;
using Api.Middleware;
using Infrastructure.Auth;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=ecommerce.db"));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IAdminUserRepository, AdminUserRepository>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenGenerator, TokenGenerator>();

builder.Services.AddScoped<GetProductsHandler>();
builder.Services.AddScoped<CreateProductHandler>();
builder.Services.AddScoped<UpdateProductHandler>();
builder.Services.AddScoped<DeleteProductHandler>();
builder.Services.AddScoped<GetOrdersHandler>();
builder.Services.AddScoped<GetOrderByIdHandler>();
builder.Services.AddScoped<CreateOrderHandler>();
builder.Services.AddScoped<UpdateOrderStatusHandler>();
builder.Services.AddScoped<LoginHandler>();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SigningKey"]!)),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<DomainExceptionMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
```

The trailing `public partial class Program { }` lets `WebApplicationFactory<Program>` target this entry point in Task 10's integration tests.

- [ ] **Step 8: Add JWT config to `appsettings.json`**

`src/Api/appsettings.json` — merge into the existing file:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Default": "Data Source=ecommerce.db"
  },
  "Jwt": {
    "Issuer": "ecommerce-clean-arch",
    "Audience": "ecommerce-clean-arch-clients",
    "SigningKey": "dev-only-signing-key-change-me-32-chars-min",
    "ExpiryMinutes": 60
  }
}
```

- [ ] **Step 9: Verify the solution builds**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 10: Commit**

```bash
git add src/Api
git commit -m "feat(api): wire controllers, JWT auth, DI, and ProblemDetails middleware"
```

---

### Task 10: Api Integration Tests

**Files:**
- Modify: `tests/Api.IntegrationTests/Api.IntegrationTests.csproj` (add packages)
- Create: `tests/Api.IntegrationTests/TestApiFactory.cs`
- Create: `tests/Api.IntegrationTests/AuthTests.cs`
- Create: `tests/Api.IntegrationTests/ProductsEndpointTests.cs`
- Create: `tests/Api.IntegrationTests/OrdersEndpointTests.cs`

**Interfaces:**
- Consumes: `Program` (Task 9, entry point), `AppDbContext` (Task 6), `AdminUser`/`PasswordHasher` (Tasks 2, 8).
- Produces: nothing consumed by later tasks — this is the outermost verification layer.

- [ ] **Step 1: Add test packages**

```bash
dotnet add tests/Api.IntegrationTests/Api.IntegrationTests.csproj package Microsoft.AspNetCore.Mvc.Testing --version 8.0.0
dotnet add tests/Api.IntegrationTests/Api.IntegrationTests.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.0
```

- [ ] **Step 2: Write `TestApiFactory`**

`tests/Api.IntegrationTests/TestApiFactory.cs`:

```csharp
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests;

public class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnectionKeeper _connectionKeeper = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connectionKeeper.Connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }
}

public class SqliteConnectionKeeper : IDisposable
{
    public Microsoft.Data.Sqlite.SqliteConnection Connection { get; }

    public SqliteConnectionKeeper()
    {
        Connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        Connection.Open();
    }

    public void Dispose() => Connection.Dispose();
}
```

- [ ] **Step 3: Write `AuthTests`**

`tests/Api.IntegrationTests/AuthTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Application.Auth;
using Domain;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.IntegrationTests;

public class AuthTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public AuthTests(TestApiFactory factory) => _factory = factory;

    private async Task SeedAdminAsync(string username, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        db.AdminUsers.Add(new AdminUser(Guid.NewGuid(), username, hasher.Hash(password)));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_token()
    {
        await SeedAdminAsync("admin1", "correct-password");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin1", password = "correct-password" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.False(string.IsNullOrEmpty(body!["token"]));
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        await SeedAdminAsync("admin2", "correct-password");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin2", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_without_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 4: Write `ProductsEndpointTests`**

`tests/Api.IntegrationTests/ProductsEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using Xunit;

namespace Api.IntegrationTests;

public class ProductsEndpointTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public ProductsEndpointTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetAll_is_publicly_accessible_and_returns_empty_list_initially()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
        Assert.NotNull(products);
    }

    [Fact]
    public async Task Create_without_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/products", new { name = "Widget", price = 9.99m, stockQuantity = 10 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 5: Write `OrdersEndpointTests`**

`tests/Api.IntegrationTests/OrdersEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using Xunit;

namespace Api.IntegrationTests;

public class OrdersEndpointTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public OrdersEndpointTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_order_for_nonexistent_product_returns_422_ProblemDetails()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = Guid.NewGuid(),
            lines = new[] { new { productId = Guid.NewGuid(), quantity = 1 } },
        });

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }
}
```

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test`
Expected: All tests across `Domain.UnitTests`, `Application.UnitTests`, and `Api.IntegrationTests` PASS.

- [ ] **Step 7: Commit**

```bash
git add tests/Api.IntegrationTests
git commit -m "test(api): add integration tests for auth, products, and orders endpoints"
```

---

### Task 11: CI Workflow

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: the full solution built by Tasks 1-10.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Write the workflow**

`.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore --configuration Release
      - name: Test
        run: dotnet test --no-build --configuration Release
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: run dotnet build and test on push and pull request"
```

---

### Task 12: Storefront Frontend

**Files:**
- Create: `frontend/storefront/index.html`
- Create: `frontend/storefront/app.js`
- Create: `frontend/storefront/style.css`

**Interfaces:**
- Consumes: `GET /api/products`, `POST /api/orders` (Task 9).
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Write `index.html`**

`frontend/storefront/index.html`:

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <title>Storefront</title>
  <link rel="stylesheet" href="style.css" />
</head>
<body>
  <h1>Storefront</h1>
  <section id="catalog"></section>
  <section id="cart">
    <h2>Cart</h2>
    <ul id="cart-items"></ul>
    <p>Total: $<span id="cart-total">0.00</span></p>
    <label>Customer name <input id="customer-name" /></label>
    <label>Customer email <input id="customer-email" /></label>
    <button id="checkout-button">Checkout</button>
    <p id="checkout-result"></p>
  </section>
  <script src="app.js"></script>
</body>
</html>
```

- [ ] **Step 2: Write `app.js`**

`frontend/storefront/app.js`:

```javascript
const API_BASE = "http://localhost:5000/api";
const cart = new Map();

async function loadCatalog() {
  const response = await fetch(`${API_BASE}/products`);
  const products = await response.json();
  const catalog = document.getElementById("catalog");
  catalog.innerHTML = products
    .map(
      (p) => `
      <div class="product">
        <span>${p.name} - $${p.price.toFixed(2)} (${p.stockQuantity} in stock)</span>
        <button data-id="${p.id}" data-price="${p.price}" class="add-to-cart">Add</button>
      </div>`
    )
    .join("");

  catalog.querySelectorAll(".add-to-cart").forEach((button) => {
    button.addEventListener("click", () => {
      const id = button.dataset.id;
      const price = parseFloat(button.dataset.price);
      const existing = cart.get(id) || { quantity: 0, price };
      cart.set(id, { quantity: existing.quantity + 1, price });
      renderCart();
    });
  });
}

function renderCart() {
  const list = document.getElementById("cart-items");
  let total = 0;
  list.innerHTML = [...cart.entries()]
    .map(([id, { quantity, price }]) => {
      total += quantity * price;
      return `<li>${id} x${quantity} - $${(quantity * price).toFixed(2)}</li>`;
    })
    .join("");
  document.getElementById("cart-total").textContent = total.toFixed(2);
}

async function checkout() {
  const lines = [...cart.entries()].map(([productId, { quantity }]) => ({
    productId,
    quantity,
  }));

  const response = await fetch(`${API_BASE}/orders`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ customerId: crypto.randomUUID(), lines }),
  });

  const result = document.getElementById("checkout-result");
  if (response.ok) {
    const order = await response.json();
    result.textContent = `Order ${order.id} created with status ${order.status}.`;
    cart.clear();
    renderCart();
    loadCatalog();
  } else {
    const problem = await response.json();
    result.textContent = `Checkout failed: ${problem.detail}`;
  }
}

document.getElementById("checkout-button").addEventListener("click", checkout);
loadCatalog();
```

- [ ] **Step 3: Write `style.css`**

`frontend/storefront/style.css`:

```css
body { font-family: sans-serif; max-width: 700px; margin: 2rem auto; }
.product { display: flex; justify-content: space-between; padding: 0.5rem 0; border-bottom: 1px solid #ddd; }
#cart { margin-top: 2rem; border-top: 2px solid #333; padding-top: 1rem; }
label { display: block; margin: 0.5rem 0; }
```

- [ ] **Step 4: Manual verification**

Run the API (`dotnet run --project src/Api`), open `frontend/storefront/index.html` directly in a browser, confirm the catalog loads, items can be added to the cart, and checkout creates an order (check the response in the browser's network tab).

- [ ] **Step 5: Commit**

```bash
git add frontend/storefront
git commit -m "feat(storefront): add catalog, cart, and checkout"
```

---

### Task 13: Admin Frontend

**Files:**
- Create: `frontend/admin/index.html`
- Create: `frontend/admin/app.js`
- Create: `frontend/admin/style.css`

**Interfaces:**
- Consumes: `POST /api/auth/login`, `GET/POST/PUT/DELETE /api/products`, `GET /api/orders`, `PATCH /api/orders/{id}/status` (Task 9).
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Write `index.html`**

`frontend/admin/index.html`:

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <title>Admin</title>
  <link rel="stylesheet" href="style.css" />
</head>
<body>
  <h1>Admin</h1>

  <section id="login-section">
    <label>Username <input id="username" /></label>
    <label>Password <input id="password" type="password" /></label>
    <button id="login-button">Log in</button>
    <p id="login-result"></p>
  </section>

  <section id="admin-section" hidden>
    <h2>Products</h2>
    <div id="products"></div>
    <h3>New product</h3>
    <label>Name <input id="new-product-name" /></label>
    <label>Price <input id="new-product-price" type="number" step="0.01" /></label>
    <label>Stock <input id="new-product-stock" type="number" /></label>
    <button id="create-product-button">Create product</button>

    <h2>Orders</h2>
    <div id="orders"></div>
  </section>

  <script src="app.js"></script>
</body>
</html>
```

- [ ] **Step 2: Write `app.js`**

`frontend/admin/app.js`:

```javascript
const API_BASE = "http://localhost:5000/api";
let token = sessionStorage.getItem("adminToken");

function authHeaders() {
  return { Authorization: `Bearer ${token}` };
}

async function login() {
  const username = document.getElementById("username").value;
  const password = document.getElementById("password").value;

  const response = await fetch(`${API_BASE}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, password }),
  });

  if (response.ok) {
    const body = await response.json();
    token = body.token;
    sessionStorage.setItem("adminToken", token);
    showAdminSection();
  } else {
    document.getElementById("login-result").textContent = "Invalid credentials.";
  }
}

function showAdminSection() {
  document.getElementById("login-section").hidden = true;
  document.getElementById("admin-section").hidden = false;
  loadProducts();
  loadOrders();
}

async function loadProducts() {
  const response = await fetch(`${API_BASE}/products`);
  const products = await response.json();
  document.getElementById("products").innerHTML = products
    .map((p) => `<div>${p.name} - $${p.price.toFixed(2)} (${p.stockQuantity} in stock)
      <button data-id="${p.id}" class="delete-product">Delete</button></div>`)
    .join("");

  document.querySelectorAll(".delete-product").forEach((button) => {
    button.addEventListener("click", async () => {
      await fetch(`${API_BASE}/products/${button.dataset.id}`, {
        method: "DELETE",
        headers: authHeaders(),
      });
      loadProducts();
    });
  });
}

async function createProduct() {
  const name = document.getElementById("new-product-name").value;
  const price = parseFloat(document.getElementById("new-product-price").value);
  const stockQuantity = parseInt(document.getElementById("new-product-stock").value, 10);

  await fetch(`${API_BASE}/products`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify({ name, price, stockQuantity }),
  });
  loadProducts();
}

async function loadOrders() {
  const response = await fetch(`${API_BASE}/orders`, { headers: authHeaders() });
  const orders = await response.json();
  const validNextStatus = { Pending: ["Paid", "Cancelled"], Paid: ["Shipped", "Cancelled"] };

  document.getElementById("orders").innerHTML = orders
    .map((o) => {
      const nextOptions = (validNextStatus[o.status] || [])
        .map((s) => `<button data-id="${o.id}" data-status="${s}" class="advance-status">${s}</button>`)
        .join(" ");
      return `<div>Order ${o.id} - ${o.status} ${nextOptions}</div>`;
    })
    .join("");

  document.querySelectorAll(".advance-status").forEach((button) => {
    button.addEventListener("click", async () => {
      await fetch(`${API_BASE}/orders/${button.dataset.id}/status`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json", ...authHeaders() },
        body: JSON.stringify({ status: button.dataset.status }),
      });
      loadOrders();
      loadProducts();
    });
  });
}

document.getElementById("login-button").addEventListener("click", login);
document.getElementById("create-product-button").addEventListener("click", createProduct);

if (token) {
  showAdminSection();
}
```

- [ ] **Step 3: Write `style.css`**

`frontend/admin/style.css`:

```css
body { font-family: sans-serif; max-width: 700px; margin: 2rem auto; }
label { display: block; margin: 0.5rem 0; }
#products div, #orders div { padding: 0.5rem 0; border-bottom: 1px solid #ddd; }
```

- [ ] **Step 4: Manual verification**

With the API running and at least one `AdminUser` seeded (see Task 14's seed note), open `frontend/admin/index.html`, log in, confirm products and orders load, create a product, and advance an order's status.

- [ ] **Step 5: Commit**

```bash
git add frontend/admin
git commit -m "feat(admin): add login, product management, and order status flow"
```

---

### Task 14: Database Seeding for Local Development and README

**Files:**
- Create: `src/Api/DevSeed.cs`
- Modify: `src/Api/Program.cs`
- Create: `README.md`

**Interfaces:**
- Consumes: `AppDbContext`, `IPasswordHasher` (Tasks 6, 8).
- Produces: nothing consumed by later tasks — this is the final task.

- [ ] **Step 1: Write a development-only seeding routine**

`src/Api/DevSeed.cs`:

```csharp
using Application.Auth;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api;

public static class DevSeed
{
    public static void EnsureSeeded(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        if (!db.AdminUsers.Any())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            db.AdminUsers.Add(new AdminUser(Guid.NewGuid(), "admin", hasher.Hash("changeme")));
        }

        if (!db.Products.Any())
        {
            db.Products.AddRange(
                new Product(Guid.NewGuid(), "Widget", 9.99m, 50),
                new Product(Guid.NewGuid(), "Gadget", 19.99m, 30),
                new Product(Guid.NewGuid(), "Gizmo", 29.99m, 15));
        }

        db.SaveChanges();
    }
}
```

- [ ] **Step 2: Call it from `Program.cs` in Development, and create the initial migration**

Add before `app.Run();` in `src/Api/Program.cs`:

```csharp
if (app.Environment.IsDevelopment())
{
    Api.DevSeed.EnsureSeeded(app.Services);
}
```

Run:

```bash
dotnet ef migrations add InitialCreate --project src/Infrastructure --startup-project src/Api
```

Expected: a `Migrations/` folder is generated under `src/Infrastructure` with an `InitialCreate` migration matching the entity configurations from Task 6.

- [ ] **Step 3: Verify the API runs and seeds correctly**

Run: `dotnet run --project src/Api`
Then in another terminal: `curl http://localhost:5000/api/products`
Expected: JSON array with the three seeded products.

- [ ] **Step 4: Write `README.md`**

`README.md`:

```markdown
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
```

- [ ] **Step 5: Run the full test suite one last time**

Run: `dotnet test`
Expected: All tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Api/DevSeed.cs src/Api/Program.cs src/Infrastructure/Migrations README.md
git commit -m "feat(api): add dev seeding and initial migration; add README"
```
