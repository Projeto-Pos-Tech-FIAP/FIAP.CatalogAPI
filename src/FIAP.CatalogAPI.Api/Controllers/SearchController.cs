using FIAP.CatalogAPI.Application.DTOs;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIAP.CatalogAPI.Api.Controllers;

/// <summary>
/// Busca avançada do catálogo em Elasticsearch, independente da consulta relacional.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private readonly IGameService _gameService;

    public SearchController(IGameService gameService) => _gameService = gameService;

    /// <summary>
    /// Busca jogos por termo livre, com tolerância a erros de digitação (fuzzy) e
    /// ordenação por relevância.
    /// </summary>
    /// <remarks>
    /// O termo casa contra título, gêneros, desenvolvedora, publicadora e descrição, com
    /// pesos diferentes — o título vale mais. Erros de digitação são tolerados por
    /// distância de edição (ex.: <c>q=couter strke</c> encontra "Counter-Strike").
    /// Sem termo, devolve o catálogo em ordem alfabética.
    ///
    /// Exemplos:
    /// <c>GET /api/search?q=witcher</c>
    /// <c>GET /api/search?q=minecaft&amp;pageSize=5</c>
    /// <c>GET /api/search?genre=RPG&amp;minPrice=50&amp;maxPrice=200</c>
    /// </remarks>
    /// <param name="query">Filtros e paginação da busca.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    [HttpGet]
    [ProducesResponseType(typeof(GameSearchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExceptionOutputDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ExceptionOutputDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] GameSearchQueryDto query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _gameService.SearchAsync(query, cancellationToken);
            return Ok(result);
        }
        catch (SearchUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ExceptionOutputDto
            {
                Message = ex.Message,
                StatusCode = StatusCodes.Status503ServiceUnavailable
            });
        }
    }

    /// <summary>
    /// Recarrega o índice de busca a partir do banco relacional.
    /// </summary>
    /// <remarks>
    /// Serve para a carga inicial (catálogo que já existia antes do Elasticsearch entrar)
    /// e para reconciliar o índice depois de uma indisponibilidade do cluster — durante a
    /// qual as escritas seguem no banco principal, mas não chegam ao índice.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    [Authorize]
    [HttpPost("reindex")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExceptionOutputDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ReindexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var indexed = await _gameService.ReindexSearchAsync(cancellationToken);
            return Ok(new { indexed, message = $"{indexed} jogo(s) reindexado(s)." });
        }
        catch (SearchUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ExceptionOutputDto
            {
                Message = ex.Message,
                StatusCode = StatusCodes.Status503ServiceUnavailable
            });
        }
    }
}
