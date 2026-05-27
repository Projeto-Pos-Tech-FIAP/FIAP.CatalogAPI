using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIAP.CatalogAPI.Domain.Entities;

[Table("LibraryGame")]
public class LibraryGame
{
    [Key]
    public int LibraryGameId { get; set; }

    public int LibraryId { get; set; }

    public int GameId { get; set; }

    public DateTime AcquiredAt { get; set; }

    public string? AcquiredFromOrderId { get; set; }

    public Library? Library { get; set; }

    public Game? Game { get; set; }

    public LibraryGame(int libraryId, int gameId, string? acquiredFromOrderId = null)
    {
        LibraryId = libraryId;
        GameId = gameId;
        AcquiredAt = DateTime.UtcNow;
        AcquiredFromOrderId = acquiredFromOrderId;
    }
}
