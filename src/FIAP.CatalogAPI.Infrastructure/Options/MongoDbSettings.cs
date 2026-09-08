namespace FIAP.CatalogAPI.Infrastructure.Options;

public class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 27017;
    public string? Username { get; set; }
    public string? Password { get; set; }

    /// <summary>Base de auditoria de alterações das entidades relacionais.</summary>
    public string DatabaseName { get; set; } = null!;
    public string CollectionName { get; set; } = "AuditLogs";

    /// <summary>
    /// Base compartilhada dos logs de eventos. Catalog, Payment e Users escrevem na
    /// mesma coleção para que o fluxo completo de uma compra possa ser reconstruído
    /// por CorrelationId em uma única consulta.
    /// </summary>
    public string EventsDatabaseName { get; set; } = "FcgEvents";
    public string EventsCollectionName { get; set; } = "EventLogs";
}
