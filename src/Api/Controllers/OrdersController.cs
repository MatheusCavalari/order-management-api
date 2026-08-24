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
        OrderStatus? filter = null;
        if (status is not null)
        {
            if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid status value",
                    Detail = $"'{status}' is not a valid order status.",
                });
            }
            filter = parsed;
        }
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
            request.CustomerName,
            request.CustomerEmail,
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
        if (!Enum.TryParse<OrderStatus>(request.Status, ignoreCase: true, out var newStatus))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid status value",
                Detail = $"'{request.Status}' is not a valid order status.",
            });
        }
        return Ok(await handler.HandleAsync(id, newStatus));
    }
}
