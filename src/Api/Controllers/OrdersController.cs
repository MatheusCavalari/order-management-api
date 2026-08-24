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

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<OrderDto>> GetById(
        Guid id,
        [FromServices] GetOrderByIdHandler handler)
    {
        var result = await handler.HandleAsync(id);
        return result is null ? NotFound() : Ok(result);
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
