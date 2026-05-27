using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIAP.CatalogAPI.Domain.Extensions;

namespace FIAP.CatalogAPI.Domain.Entities;

[Table("Library")]
public class Library : SoftDelete
{
    [Key]
    public int LibraryId { get; set; }

    public Guid UserGuid { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsActive { get; set; }

    public ICollection<LibraryGame> LibraryGames { get; set; } = new List<LibraryGame>();

    public Library(Guid userGuid)
    {
        UserGuid = userGuid;
        CreatedAt = DateTime.UtcNow;
        IsActive = true;
    }
}
