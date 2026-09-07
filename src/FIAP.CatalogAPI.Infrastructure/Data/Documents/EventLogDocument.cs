using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FIAP.CatalogAPI.Infrastructure.Data.Documents;

/// <summary>
/// Forma do <see cref="Domain.Entities.EventLog"/> dentro do MongoDB. Existe separada da
/// entidade de domínio para que o payload possa ser gravado como subdocumento (consultável
/// por campo) sem vazar tipos do driver para o domínio.
/// </summary>
internal sealed class EventLogDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonIgnoreIfNull]
    public string? Id { get; set; }

    [BsonElement("service")]
    public string Service { get; set; } = null!;

    [BsonElement("eventType")]
    public string EventType { get; set; } = null!;

    [BsonElement("direction")]
    public string Direction { get; set; } = null!;

    [BsonElement("topic")]
    public string Topic { get; set; } = null!;

    [BsonElement("correlationId")]
    [BsonIgnoreIfNull]
    public string? CorrelationId { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = null!;

    [BsonElement("error")]
    [BsonIgnoreIfNull]
    public string? Error { get; set; }

    [BsonElement("payload")]
    [BsonIgnoreIfNull]
    public BsonDocument? Payload { get; set; }

    [BsonElement("occurredAt")]
    public DateTime OccurredAt { get; set; }
}
