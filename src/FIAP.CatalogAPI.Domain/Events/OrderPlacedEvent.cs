namespace FIAP.CatalogAPI.Domain.Events;

public record OrderPlacedEvent(
    Guid UserId,
    int GameId,
    decimal Price,
    string CorrelationId
);
