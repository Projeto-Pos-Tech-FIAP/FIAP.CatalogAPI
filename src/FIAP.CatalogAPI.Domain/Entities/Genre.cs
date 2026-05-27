using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIAP.CatalogAPI.Domain.Extensions;

namespace FIAP.CatalogAPI.Domain.Entities;

[Table("Genre")]
public class Genre : SoftDelete
{
    [Key]
    public int GenreId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    public ICollection<GameGenre> GameGenres { get; set; } = new List<GameGenre>();
}
