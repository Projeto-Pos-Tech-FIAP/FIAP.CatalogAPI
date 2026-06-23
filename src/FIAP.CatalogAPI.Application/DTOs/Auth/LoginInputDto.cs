using System.ComponentModel.DataAnnotations;

namespace FIAP.CatalogAPI.Application.DTOs.Auth;

public class LoginInputDto
{
    [Required]
    public string Username { get; set; } = null!;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = null!;
}
