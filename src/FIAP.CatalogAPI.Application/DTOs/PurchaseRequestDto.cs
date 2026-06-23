using System.ComponentModel.DataAnnotations;

namespace FIAP.CatalogAPI.Application.DTOs;

public class PurchaseRequestDto
{
    [Required]
    public int GameId { get; set; }
}
