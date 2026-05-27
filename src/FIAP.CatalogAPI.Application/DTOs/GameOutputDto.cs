namespace FIAP.CatalogAPI.Application.DTOs;

public class GameOutputDto
{
    public int GameId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Developer { get; set; }
    public string? Publisher { get; set; }
    public DateTime ReleaseDate { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<GenreOutputDto> Genres { get; set; } = new();
}
