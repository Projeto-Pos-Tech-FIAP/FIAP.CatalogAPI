using FIAP.CatalogAPI.Infrastructure.Options;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FIAP.CatalogAPI.Infrastructure.Data.Mongo;

/// <summary>
/// Dono do único <see cref="IMongoClient"/> do processo. O driver já mantém pool de
/// conexões internamente e é thread-safe, então instanciá-lo por requisição (como era
/// feito antes) só desperdiçava conexões.
/// </summary>
public class MongoContext
{
    private readonly IMongoClient _client;
    private readonly MongoDbSettings _settings;

    public MongoContext(IOptions<MongoDbSettings> options)
    {
        _settings = options.Value;

        var clientSettings = new MongoClientSettings
        {
            Server = new MongoServerAddress(_settings.Host, _settings.Port)
        };

        // Mongo local sem autenticação (docker sem MONGO_INITDB_ROOT_*) não aceita credenciais.
        if (!string.IsNullOrWhiteSpace(_settings.Username))
        {
            clientSettings.Credential = MongoCredential.CreateCredential(
                "admin", _settings.Username, _settings.Password);
        }

        _client = new MongoClient(clientSettings);
    }

    public IMongoCollection<T> GetCollection<T>(string databaseName, string collectionName) =>
        _client.GetDatabase(databaseName).GetCollection<T>(collectionName);

    public IMongoCollection<T> GetAuditCollection<T>() =>
        GetCollection<T>(_settings.DatabaseName, _settings.CollectionName);

    public IMongoCollection<T> GetEventLogCollection<T>() =>
        GetCollection<T>(_settings.EventsDatabaseName, _settings.EventsCollectionName);
}
