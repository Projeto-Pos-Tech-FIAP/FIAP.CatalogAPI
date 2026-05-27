using System.ComponentModel.DataAnnotations;

namespace FIAP.CatalogAPI.Application.DTOs;

public class GameUpdateDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [MaxLength(200)]
    public string? Developer { get; set; }

    [MaxLength(200)]
    public string? Publisher { get; set; }

    public DateTime? ReleaseDate { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Preço não pode ser negativo.")]
    public decimal BasePrice { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid UpdatedBy { get; set; }

    public List<int> GenreIds { get; set; } = new();
}
