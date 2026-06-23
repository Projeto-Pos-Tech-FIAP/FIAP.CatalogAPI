using FIAP.CatalogAPI.Application.DTOs;
using FIAP.CatalogAPI.Application.DTOs.Auth;
using FIAP.CatalogAPI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIAP.CatalogAPI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IKeycloakService _keycloakService;

    public AuthController(IKeycloakService keycloakService)
    {
        _keycloakService = keycloakService ?? throw new ArgumentNullException(nameof(keycloakService));
    }

    /// <summary>
    /// Autentica um usuário e retorna o access token do Keycloak.
    /// </summary>
    /// <param name="loginRequest">Credenciais do usuário (username e password)</param>
    /// <returns>JWT access token</returns>
    /// <response code="200">Retorna o token de autenticação</response>
    /// <response code="401">Usuário ou senha inválidos</response>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginOutputDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExceptionOutputDto), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromForm] LoginInputDto loginRequest)
    {
        try
        {
            var token = await _keycloakService.GetTokenAsync(loginRequest.Username, loginRequest.Password);
            return Ok(token);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new ExceptionOutputDto { Message = "Invalid username or password", StatusCode = 401 });
        }
    }

    /// <summary>
    /// Renova um access token expirado usando o refresh token.
    /// </summary>
    /// <param name="refreshToken">Refresh token</param>
    /// <returns>Novo access token</returns>
    /// <response code="200">Retorna o novo token</response>
    /// <response code="400">Refresh token inválido ou expirado</response>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginOutputDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExceptionOutputDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refresh([FromBody] string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return BadRequest(new ExceptionOutputDto { Message = "Refresh token must be provided.", StatusCode = 400 });

        try
        {
            var token = await _keycloakService.RefreshTokenAsync(refreshToken);
            return Ok(token);
        }
        catch (UnauthorizedAccessException)
        {
            return BadRequest(new ExceptionOutputDto { Message = "Invalid or expired refresh token.", StatusCode = 400 });
        }
    }
}
