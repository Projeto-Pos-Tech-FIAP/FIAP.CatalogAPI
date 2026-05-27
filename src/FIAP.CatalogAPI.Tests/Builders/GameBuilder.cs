using FIAP.CatalogAPI.Domain.Entities;

namespace FIAP.CatalogAPI.Tests.Builders;

public class GameBuilder
{
    private int _gameId = 1;
    private string _title = "Test Game";
    private decimal _basePrice = 59.99m;
    private Guid _createdBy = Guid.NewGuid();
    private string? _description = "A test game description";
    private bool _isActive = true;

    public GameBuilder WithGameId(int id) { _gameId = id; return this; }
    public GameBuilder WithTitle(string title) { _title = title; return this; }
    public GameBuilder WithPrice(decimal price) { _basePrice = price; return this; }
    public GameBuilder WithCreatedBy(Guid userId) { _createdBy = userId; return this; }
    public GameBuilder WithDescription(string? description) { _description = description; return this; }
    public GameBuilder Inactive() { _isActive = false; return this; }

    public Game Build()
    {
        var game = new Game(_title, _basePrice, _createdBy, _description, isActive: _isActive);

        // Set the Id via reflection since EF would normally handle it
        typeof(Game).GetProperty(nameof(Game.GameId))!.SetValue(game, _gameId);

        return game;
    }

    public static GameBuilder New() => new();
}
