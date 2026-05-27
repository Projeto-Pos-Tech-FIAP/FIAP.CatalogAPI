using System.ComponentModel.DataAnnotations;

namespace FIAP.CatalogAPI.Application.DTOs;

public class PurchaseRequestDto
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public int GameId { get; set; }
}
