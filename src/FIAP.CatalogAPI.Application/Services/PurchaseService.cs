using FIAP.CatalogAPI.Application.DTOs;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Domain.Events;
using FIAP.CatalogAPI.Domain.Exceptions;
using FIAP.CatalogAPI.Domain.Interfaces;

namespace FIAP.CatalogAPI.Application.Services;

public class PurchaseService : IPurchaseService
{
    private readonly IGameRepository _gameRepository;
    private readonly ILibraryRepository _libraryRepository;
    private readonly IKafkaProducerService _kafkaProducer;

    public PurchaseService(
        IGameRepository gameRepository,
        ILibraryRepository libraryRepository,
        IKafkaProducerService kafkaProducer)
    {
        _gameRepository = gameRepository;
        _libraryRepository = libraryRepository;
        _kafkaProducer = kafkaProducer;
    }

    public async Task<PurchaseResponseDto> InitiatePurchaseAsync(PurchaseRequestDto dto, CancellationToken cancellationToken = default)
    {
        var game = await _gameRepository.GetByIdAsync(dto.GameId)
            ?? throw new NotFoundException("Game", dto.GameId);

        if (!game.IsActive)
            throw new InvalidOperationException($"O jogo '{game.Title}' não está disponível para compra.");

        var alreadyOwns = await _libraryRepository.UserOwnsGameAsync(dto.UserId, dto.GameId);
        if (alreadyOwns)
            throw new GameAlreadyOwnedException(dto.UserId, dto.GameId);

        var correlationId = Guid.NewGuid().ToString();

        var @event = new OrderPlacedEvent(dto.UserId, dto.GameId, game.BasePrice, correlationId);

        await _kafkaProducer.PublishAsync("order-placed", correlationId, @event, cancellationToken);

        return new PurchaseResponseDto
        {
            CorrelationId = correlationId,
            UserId = dto.UserId,
            GameId = dto.GameId,
            Price = game.BasePrice,
            Status = "Pending",
            Message = $"Pedido de compra do jogo '{game.Title}' iniciado. Aguardando processamento do pagamento."
        };
    }
}
