using FIAP.CatalogAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace FIAP.CatalogAPI.Infrastructure.Data.Audit;

internal sealed class AuditEntryCapture
{
    private readonly EntityEntry _entry;

    public string EntityName { get; }
    public string Action { get; }
    public Dictionary<string, object?> OldValues { get; } = new();
    public Dictionary<string, object?> NewValues { get; } = new();
    public List<PropertyEntry> TemporaryProperties { get; } = new();
    public bool HasTemporaryProperties => TemporaryProperties.Count > 0;

    public AuditEntryCapture(EntityEntry entry)
    {
        _entry = entry;
        EntityName = entry.Metadata.ClrType.Name;
        Action = ResolveAction(entry);

        foreach (var property in entry.Properties)
        {
            if (property.IsTemporary)
            {
                TemporaryProperties.Add(property);
                continue;
            }

            var propertyName = property.Metadata.Name;

            if (entry.State == EntityState.Added)
                NewValues[propertyName] = property.CurrentValue;
            else if (entry.State == EntityState.Deleted)
                OldValues[propertyName] = property.OriginalValue;
            else if (entry.State == EntityState.Modified)
            {
                OldValues[propertyName] = property.OriginalValue;
                NewValues[propertyName] = property.CurrentValue;
            }
        }
    }

    public AuditLog ToAuditLog()
    {
        foreach (var prop in TemporaryProperties)
            NewValues[prop.Metadata.Name] = prop.CurrentValue;

        return new AuditLog
        {
            EntityName = EntityName,
            Action = Action,
            EntityId = ResolveEntityId(),
            OldValues = OldValues.Count > 0 ? OldValues : null,
            NewValues = NewValues.Count > 0 ? NewValues : null,
            UserId = ResolveUserId(),
            Timestamp = DateTime.UtcNow
        };
    }

    private static string ResolveAction(EntityEntry entry)
    {
        if (entry.State == EntityState.Added) return "Created";
        if (entry.State == EntityState.Deleted) return "Deleted";

        var isDeletedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");
        if (isDeletedProp?.CurrentValue is true && isDeletedProp.IsModified)
            return "SoftDeleted";

        return "Updated";
    }

    private string ResolveEntityId()
    {
        var pkValues = _entry.Metadata.FindPrimaryKey()?.Properties
            .Select(p => _entry.Property(p.Name).CurrentValue?.ToString())
            .ToArray();

        return pkValues is { Length: > 0 } ? string.Join("|", pkValues) : string.Empty;
    }

    private Guid? ResolveUserId()
    {
        var userProp = NewValues.ContainsKey("UpdatedBy") ? "UpdatedBy"
                     : NewValues.ContainsKey("CreatedBy") ? "CreatedBy"
                     : null;

        if (userProp is null) return null;
        return NewValues[userProp] is Guid guid ? guid : null;
    }
}
