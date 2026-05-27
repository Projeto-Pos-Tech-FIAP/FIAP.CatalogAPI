namespace FIAP.CatalogAPI.Domain.Entities;

public class AuditLog
{
    public string Id { get; set; } = null!;
    public string EntityName { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public Dictionary<string, object?>? OldValues { get; set; }
    public Dictionary<string, object?>? NewValues { get; set; }
    public Guid? UserId { get; set; }
    public DateTime Timestamp { get; set; }
}
