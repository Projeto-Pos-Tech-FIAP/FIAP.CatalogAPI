namespace FIAP.CatalogAPI.Domain.Entities;

/// <summary>
/// Registro de um evento de integração que trafegou pelo Kafka (publicado ou consumido).
/// Persistido no MongoDB: o payload varia por tipo de evento e o volume cresce sem limite,
/// então um documento schemaless serve melhor que uma tabela relacional.
/// </summary>
public class EventLog
{
    public string? Id { get; set; }

    /// <summary>Microsserviço que gerou o registro (ex.: "CatalogAPI").</summary>
    public string Service { get; set; } = null!;

    /// <summary>Nome do evento (ex.: "OrderPlacedEvent").</summary>
    public string EventType { get; set; } = null!;

    /// <summary><see cref="EventLogDirection"/> — se o serviço publicou ou consumiu o evento.</summary>
    public string Direction { get; set; } = null!;

    public string Topic { get; set; } = null!;

    /// <summary>Id que amarra todos os eventos de um mesmo fluxo de compra.</summary>
    public string? CorrelationId { get; set; }

    /// <summary><see cref="EventLogStatus"/> — resultado do processamento do evento.</summary>
    public string Status { get; set; } = EventLogStatus.Success;

    public string? Error { get; set; }

    /// <summary>Corpo do evento em JSON. Gravado como subdocumento no Mongo.</summary>
    public string? PayloadJson { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

public static class EventLogDirection
{
    public const string Published = "Published";
    public const string Consumed = "Consumed";
}

public static class EventLogStatus
{
    public const string Success = "Success";
    public const string Failed = "Failed";
}
