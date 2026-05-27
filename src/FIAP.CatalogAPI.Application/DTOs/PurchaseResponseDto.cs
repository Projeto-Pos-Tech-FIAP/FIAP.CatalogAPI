namespace FIAP.CatalogAPI.Application.DTOs;

public class PurchaseResponseDto
{
    public string CorrelationId { get; set; } = null!;
    public Guid UserId { get; set; }
    public int GameId { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "Pending";
    public string Message { get; set; } = null!;
}
