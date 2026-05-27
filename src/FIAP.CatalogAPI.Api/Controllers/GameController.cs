using FIAP.CatalogAPI.Application.DTOs;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace FIAP.CatalogAPI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GameController : ControllerBase
{
    private readonly IGameService _gameService;

    public GameController(IGameService gameService) => _gameService = gameService;

    /// <summary>
    /// Retorna todos os jogos disponíveis.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<GameOutputDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync()
    {
        var games = await _gameService.GetAllAsync();
        return Ok(games);
    }

    /// <summary>
    /// Retorna um jogo pelo ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GameOutputDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExceptionOutputDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(int id)
    {
        try
        {
            var game = await _gameService.GetByIdAsync(id);
            return Ok(game);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ExceptionOutputDto { Message = ex.Message, StatusCode = 404 });
        }
    }

    /// <summary>
    /// Cria um novo jogo.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(GameOutputDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ExceptionOutputDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync([FromBody] GameInputDto dto)
    {
        try
        {
            var game = await _gameService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetByIdAsync), new { id = game.GameId }, game);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ExceptionOutputDto { Message = ex.Message, StatusCode = 404 });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ExceptionOutputDto { Message = ex.Message, StatusCode = 400 });
        }
    }

    /// <summary>
    /// Atualiza um jogo existente.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(GameOutputDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExceptionOutputDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ExceptionOutputDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] GameUpdateDto dto)
    {
        try
        {
            var game = await _gameService.UpdateAsync(id, dto);
            return Ok(game);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ExceptionOutputDto { Message = ex.Message, StatusCode = 404 });
        }
    }

    /// <summary>
    /// Remove um jogo (soft delete).
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ExceptionOutputDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        try
        {
            await _gameService.DeleteAsync(id);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ExceptionOutputDto { Message = ex.Message, StatusCode = 404 });
        }
    }
}
