using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIAP.CatalogAPI.Domain.Extensions;

namespace FIAP.CatalogAPI.Domain.Entities;

[Table("Game")]
public class Game : SoftDelete
{
    [Key]
    public int GameId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [MaxLength(200)]
    public string? Developer { get; set; }

    [MaxLength(200)]
    public string? Publisher { get; set; }

    public DateTime ReleaseDate { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal BasePrice { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public ICollection<GameGenre> GameGenres { get; set; } = new List<GameGenre>();

    public ICollection<LibraryGame> LibraryGames { get; set; } = new List<LibraryGame>();

    public Game(string title, decimal basePrice, Guid createdBy, string? description = null,
        string? developer = null, string? publisher = null, DateTime? releaseDate = null, bool isActive = true)
    {
        Title = title;
        Description = description;
        Developer = developer;
        Publisher = publisher;
        ReleaseDate = releaseDate ?? DateTime.UtcNow;
        BasePrice = basePrice;
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy;
        IsActive = isActive;
    }

    public void Update(string title, decimal basePrice, Guid updatedBy, string? description = null,
        string? developer = null, string? publisher = null, DateTime? releaseDate = null, bool isActive = true)
    {
        Title = title;
        Description = description;
        Developer = developer;
        Publisher = publisher;
        if (releaseDate.HasValue) ReleaseDate = releaseDate.Value;
        BasePrice = basePrice;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}
