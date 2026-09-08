using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Domain.Entities;
using FIAP.CatalogAPI.Infrastructure.Data.Documents;
using FIAP.CatalogAPI.Infrastructure.Data.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace FIAP.CatalogAPI.Infrastructure.Data.Services;

/// <summary>
/// Persiste os logs de eventos de integração no MongoDB (requisito de persistência
/// poliglota da Fase 3). Falha ao gravar nunca derruba o fluxo de negócio: o log é
/// observabilidade, não parte da transação.
/// </summary>
public class MongoEventLogService : IEventLogService
{
    private static readonly JsonWriterSettings PayloadJsonSettings =
        new() { OutputMode = JsonOutputMode.RelaxedExtendedJson };

    private readonly IMongoCollection<EventLogDocument> _collection;
    private readonly ILogger<MongoEventLogService> _logger;

    public MongoEventLogService(MongoContext context, ILogger<MongoEventLogService> logger)
    {
        _collection = context.GetEventLogCollection<EventLogDocument>();
        _logger = logger;

        EnsureIndexes();
    }

    public async Task LogAsync(EventLog eventLog, CancellationToken cancellationToken = default)
    {
        try
        {
            await _collection.InsertOneAsync(ToDocument(eventLog), cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Falha ao gravar EventLog no MongoDB | EventType: {EventType} | CorrelationId: {CorrelationId}",
                eventLog.EventType, eventLog.CorrelationId);
        }
    }

    public async Task<IReadOnlyList<EventLog>> QueryAsync(
        string? correlationId = null,
        string? eventType = null,
        string? service = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<EventLogDocument>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(correlationId))
            filter &= builder.Eq(d => d.CorrelationId, correlationId);

        if (!string.IsNullOrWhiteSpace(eventType))
            filter &= builder.Eq(d => d.EventType, eventType);

        if (!string.IsNullOrWhiteSpace(service))
            filter &= builder.Eq(d => d.Service, service);

        var documents = await _collection
            .Find(filter)
            .SortByDescending(d => d.OccurredAt)
            .Limit(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

        return documents.Select(ToEntity).ToList();
    }

    private void EnsureIndexes()
    {
        try
        {
            var keys = Builders<EventLogDocument>.IndexKeys;

            _collection.Indexes.CreateMany(
            [
                new CreateIndexModel<EventLogDocument>(
                    keys.Ascending(d => d.CorrelationId).Descending(d => d.OccurredAt)),
                new CreateIndexModel<EventLogDocument>(
                    keys.Descending(d => d.OccurredAt))
            ]);
        }
        catch (Exception ex)
        {
            // Sem índice a coleção ainda funciona (collection scan); não vale impedir o start.
            _logger.LogWarning(ex, "Não foi possível criar os índices da coleção de EventLogs.");
        }
    }

    private static EventLogDocument ToDocument(EventLog log) => new()
    {
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

    private static EventLog ToEntity(EventLogDocument doc) => new()
    {
        Id = doc.Id,
        Service = doc.Service,
        EventType = doc.EventType,
        Direction = doc.Direction,
        Topic = doc.Topic,
        CorrelationId = doc.CorrelationId,
        Status = doc.Status,
        Error = doc.Error,
        PayloadJson = doc.Payload?.ToJson(PayloadJsonSettings),
        OccurredAt = doc.OccurredAt
    };

    private static BsonDocument? ParsePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return null;

        try
        {
            return BsonDocument.Parse(payloadJson);
        }
        catch (Exception)
        {
            // Payload que não é um objeto JSON (string solta, array) ainda tem valor como texto.
            return new BsonDocument { { "raw", payloadJson } };
        }
    }
}
