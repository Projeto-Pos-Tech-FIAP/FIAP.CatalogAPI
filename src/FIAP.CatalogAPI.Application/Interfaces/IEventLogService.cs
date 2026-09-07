using FIAP.CatalogAPI.Domain.Entities;

namespace FIAP.CatalogAPI.Application.Interfaces;

public interface IEventLogService
{
    Task LogAsync(EventLog eventLog, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventLog>> QueryAsync(
        string? correlationId = null,
        string? eventType = null,
        string? service = null,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
