namespace FIAP.CatalogAPI.Infrastructure.Options;

public class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string GroupId { get; set; } = "catalog-api-group";
    public string TopicOrderPlaced { get; set; } = "order-placed";
    public string TopicPaymentProcessed { get; set; } = "payment-processed";
}
