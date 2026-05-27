namespace FIAP.CatalogAPI.Domain.Events;

public record PaymentProcessedEvent(
    string CorrelationId,
    Guid UserId,
    int GameId,
    string Status,
    string? Reason = null
);
