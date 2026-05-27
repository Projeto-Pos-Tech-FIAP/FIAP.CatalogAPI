namespace FIAP.CatalogAPI.Infrastructure.Options;

public class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 27017;
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string CollectionName { get; set; } = "AuditLogs";
}
