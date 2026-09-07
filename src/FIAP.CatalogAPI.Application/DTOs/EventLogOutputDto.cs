using System.Text.Json;

namespace FIAP.CatalogAPI.Application.DTOs;

public class EventLogOutputDto
{
    public string? Id { get; set; }
    public string Service { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string Direction { get; set; } = null!;
    public string Topic { get; set; } = null!;
    public string? CorrelationId { get; set; }
    public string Status { get; set; } = null!;
    public string? Error { get; set; }

    /// <summary>Corpo do evento, devolvido como JSON aninhado (não como string escapada).</summary>
    public JsonElement? Payload { get; set; }

    public DateTime OccurredAt { get; set; }
}
