using FIAP.CatalogAPI.Domain.Entities;

namespace FIAP.CatalogAPI.Application.Interfaces;

public interface IAuditService
{
    Task SaveAuditLogsAsync(IEnumerable<AuditLog> logs, CancellationToken cancellationToken = default);
}
