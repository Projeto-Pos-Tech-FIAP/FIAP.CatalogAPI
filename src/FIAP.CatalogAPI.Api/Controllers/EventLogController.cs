using System.Text.Json;
using FIAP.CatalogAPI.Application.DTOs;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIAP.CatalogAPI.Api.Controllers;

/// <summary>
/// Leitura dos logs de eventos de integração gravados no MongoDB por CatalogAPI,
/// PaymentAPI e UsersAPI.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EventLogController : ControllerBase
{
    private readonly IEventLogService _eventLogService;

    public EventLogController(IEventLogService eventLogService) => _eventLogService = eventLogService;

    /// <summary>
    /// Lista os eventos mais recentes, opcionalmente filtrados.
    /// </summary>
    /// <param name="correlationId">Amarra todos os eventos de um mesmo fluxo de compra.</param>
    /// <param name="eventType">Ex.: OrderPlacedEvent, PaymentProcessedEvent.</param>
    /// <param name="service">Ex.: CatalogAPI, PaymentAPI, UsersAPI.</param>
    /// <param name="limit">Quantidade máxima de documentos (1 a 500).</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventLogOutputDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? correlationId,
        [FromQuery] string? eventType,
        [FromQuery] string? service,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var logs = await _eventLogService.QueryAsync(correlationId, eventType, service, limit, cancellationToken);
        return Ok(logs.Select(ToDto).ToList());
    }

    /// <summary>
    /// Retorna o rastro completo de um fluxo de compra, do mais antigo ao mais recente.
    /// </summary>
    /// <param name="correlationId">Id que amarra os eventos do fluxo.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    [HttpGet("{correlationId}")]
    [ProducesResponseType(typeof(List<EventLogOutputDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var logs = await _eventLogService.QueryAsync(correlationId, limit: 500, cancellationToken: cancellationToken);

        if (logs.Count == 0)
            return NotFound(new ExceptionOutputDto
            {
                Message = $"Nenhum evento encontrado para o CorrelationId '{correlationId}'.",
                StatusCode = 404
            });

        return Ok(logs.OrderBy(l => l.OccurredAt).Select(ToDto).ToList());
    }

    private static EventLogOutputDto ToDto(EventLog log) => new()
    {
        Id = log.Id,
        Service = log.Service,
        EventType = log.EventType,
        Direction = log.Direction,
        Topic = log.Topic,
        CorrelationId = log.CorrelationId,
        Status = log.Status,
        Error = log.Error,
        Payload = ParsePayload(log.PayloadJson),
        OccurredAt = log.OccurredAt
    };

    private static JsonElement? ParsePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return null;

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            // Clone: o JsonElement não pode sobreviver ao JsonDocument que o originou.
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
