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
