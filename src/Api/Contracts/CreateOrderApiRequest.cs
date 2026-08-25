using System.ComponentModel.DataAnnotations;

namespace Api.Contracts;

public record CreateOrderLineApiRequest(Guid ProductId, [Range(1, int.MaxValue)] int Quantity);

public record CreateOrderApiRequest(
    [Required, StringLength(200, MinimumLength = 1)] string CustomerName,
    [Required, EmailAddress, StringLength(200)] string CustomerEmail,
    [Required, MinLength(1)] IReadOnlyList<CreateOrderLineApiRequest> Lines);
