using Confluent.Kafka;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Domain.Entities;
using FIAP.CatalogAPI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FIAP.CatalogAPI.Infrastructure.Kafka;

public class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private const string ServiceName = "CatalogAPI";

    private readonly IProducer<string, string> _producer;
    private readonly IEventLogService _eventLogService;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(
        IOptions<KafkaSettings> settings,
        IEventLogService eventLogService,
        ILogger<KafkaProducerService> logger)
    {
        _eventLogService = eventLogService;
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = settings.Value.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 500
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(message);

        var kafkaMessage = new Message<string, string>
        {
            Key = key,
            Value = json
        };

        try
        {
            var result = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);

            _logger.LogInformation(
                "Mensagem publicada no tópico '{Topic}' | Partition: {Partition} | Offset: {Offset}",
                topic, result.Partition.Value, result.Offset.Value);

            await LogEventAsync<T>(topic, key, json, EventLogStatus.Success, error: null, cancellationToken);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Falha ao publicar mensagem no tópico '{Topic}' com key '{Key}'", topic, key);

            await LogEventAsync<T>(topic, key, json, EventLogStatus.Failed, ex.Error.Reason, cancellationToken);
            throw;
        }
    }

    // O log de evento vai para o MongoDB e não participa da transação de negócio — por isso
    // o CancellationToken da requisição não é repassado quando a publicação falhou.
    private Task LogEventAsync<T>(string topic, string key, string payloadJson, string status, string? error, CancellationToken cancellationToken) =>
        _eventLogService.LogAsync(new EventLog
        {
            Service = ServiceName,
            EventType = typeof(T).Name,
            Direction = EventLogDirection.Published,
            Topic = topic,
            CorrelationId = key,
            Status = status,
            Error = error,
            PayloadJson = payloadJson,
            OccurredAt = DateTime.UtcNow
        }, status == EventLogStatus.Success ? cancellationToken : CancellationToken.None);

    public void Dispose() => _producer.Dispose();
}
