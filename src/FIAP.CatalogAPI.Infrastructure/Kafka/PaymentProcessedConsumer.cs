using Confluent.Kafka;
using DomainEntities = FIAP.CatalogAPI.Domain.Entities;
using FIAP.CatalogAPI.Domain.Events;
using FIAP.CatalogAPI.Domain.Interfaces;
using FIAP.CatalogAPI.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FIAP.CatalogAPI.Infrastructure.Kafka;

/// <summary>
/// Background service que consome PaymentProcessedEvent do Kafka.
/// Quando pagamento for Approved, adiciona o jogo à biblioteca do usuário.
/// </summary>
public class PaymentProcessedConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaSettings _settings;
    private readonly ILogger<PaymentProcessedConsumer> _logger;

    public PaymentProcessedConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaSettings> settings,
        ILogger<PaymentProcessedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PaymentProcessedConsumer iniciado. Tópico: {Topic}", _settings.TopicPaymentProcessed);

        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka consumer error: {Reason}", e.Reason))
            .Build();

        consumer.Subscribe(_settings.TopicPaymentProcessed);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);

                if (result?.Message?.Value is null) continue;

                _logger.LogInformation(
                    "Mensagem recebida | Tópico: {Topic} | Partition: {Partition} | Offset: {Offset}",
                    result.Topic, result.Partition.Value, result.Offset.Value);

                var @event = JsonSerializer.Deserialize<PaymentProcessedEvent>(result.Message.Value);

                if (@event is null)
                {
                    _logger.LogWarning("Mensagem inválida recebida no tópico {Topic}", result.Topic);
                    consumer.Commit(result);
                    continue;
                }

                await ProcessPaymentEventAsync(@event, stoppingToken);

                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Erro ao consumir mensagem do Kafka");
                await Task.Delay(2000, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao processar PaymentProcessedEvent");
                await Task.Delay(2000, stoppingToken);
            }
        }

        consumer.Close();
        _logger.LogInformation("PaymentProcessedConsumer encerrado.");
    }

    private async Task ProcessPaymentEventAsync(PaymentProcessedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processando PaymentProcessedEvent | CorrelationId: {CorrelationId} | UserId: {UserId} | GameId: {GameId} | Status: {Status}",
            @event.CorrelationId, @event.UserId, @event.GameId, @event.Status);

        if (!string.Equals(@event.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Pagamento recusado para CorrelationId: {CorrelationId}. Motivo: {Reason}",
                @event.CorrelationId, @event.Reason ?? "não informado");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var libraryRepository = scope.ServiceProvider.GetRequiredService<ILibraryRepository>();

        var alreadyOwns = await libraryRepository.UserOwnsGameAsync(@event.UserId, @event.GameId);
        if (alreadyOwns)
        {
            _logger.LogWarning(
                "Usuário {UserId} já possui o jogo {GameId}. Ignorando evento duplicado.",
                @event.UserId, @event.GameId);
            return;
        }

        var library = await libraryRepository.GetByUserGuidAsync(@event.UserId);

        if (library is null)
        {
            library = new DomainEntities.Library(@event.UserId);
            library = await libraryRepository.AddAsync(library);
            _logger.LogInformation("Biblioteca criada para usuário {UserId}", @event.UserId);
        }

        var libraryGame = new DomainEntities.LibraryGame(library.LibraryId, @event.GameId, @event.CorrelationId);
        await libraryRepository.AddGameAsync(libraryGame);

        _logger.LogInformation(
            "Jogo {GameId} adicionado à biblioteca do usuário {UserId} via pedido {CorrelationId}",
            @event.GameId, @event.UserId, @event.CorrelationId);
    }
}
