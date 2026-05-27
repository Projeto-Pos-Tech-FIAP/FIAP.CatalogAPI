using System.ComponentModel.DataAnnotations.Schema;

namespace FIAP.CatalogAPI.Domain.Entities;

[Table("GameGenre")]
public class GameGenre
{
    public int GameId { get; set; }
    public int GenreId { get; set; }

    public Game? Game { get; set; }
    public Genre? Genre { get; set; }
}
