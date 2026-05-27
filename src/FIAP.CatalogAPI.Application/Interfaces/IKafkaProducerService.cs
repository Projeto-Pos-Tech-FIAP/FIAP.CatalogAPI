namespace FIAP.CatalogAPI.Application.Interfaces;

public interface IKafkaProducerService
{
    Task PublishAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default);
}
