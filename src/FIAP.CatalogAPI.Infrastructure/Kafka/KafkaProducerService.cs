using Confluent.Kafka;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FIAP.CatalogAPI.Infrastructure.Kafka;

public class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(IOptions<KafkaSettings> settings, ILogger<KafkaProducerService> logger)
    {
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
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Falha ao publicar mensagem no tópico '{Topic}' com key '{Key}'", topic, key);
            throw;
        }
    }

    public void Dispose() => _producer.Dispose();
}
