using FIAP.CatalogAPI.Application.DTOs.Auth;

namespace FIAP.CatalogAPI.Application.Interfaces;

public interface IKeycloakService
{
    Task<LoginOutputDto> GetTokenAsync(string username, string password);
    Task<LoginOutputDto> RefreshTokenAsync(string refreshToken);
}
