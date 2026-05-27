namespace FIAP.CatalogAPI.Application.DTOs;

public class ExceptionOutputDto
{
    public string Message { get; set; } = null!;
    public string? Details { get; set; }
    public int StatusCode { get; set; }
}
