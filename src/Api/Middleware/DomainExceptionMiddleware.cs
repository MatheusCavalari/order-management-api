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
