using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Domain.Entities;
using FIAP.CatalogAPI.Infrastructure.Data.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace FIAP.CatalogAPI.Infrastructure.Data.Services;

public class MongoAuditService : IAuditService
{
    private readonly IMongoCollection<AuditLog> _collection;

    static MongoAuditService()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(AuditLog)))
        {
            BsonClassMap.RegisterClassMap<AuditLog>(cm =>
            {
                cm.AutoMap();
                cm.MapIdMember(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.ObjectId));
            });
        }
    }

    public MongoAuditService(MongoContext context)
    {
        _collection = context.GetAuditCollection<AuditLog>();
    }

    public async Task SaveAuditLogsAsync(IEnumerable<AuditLog> logs, CancellationToken cancellationToken = default)
    {
        var logList = logs.ToList();
        if (logList.Count == 0) return;
        await _collection.InsertManyAsync(logList, cancellationToken: cancellationToken);
    }
}
