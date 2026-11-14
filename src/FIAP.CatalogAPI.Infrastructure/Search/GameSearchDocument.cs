using System.Text.Json.Serialization;
using FIAP.CatalogAPI.Domain.Entities;

namespace FIAP.CatalogAPI.Infrastructure.Search;

/// <summary>
/// Projeção do jogo no índice de busca. É achatada de propósito: os gêneros entram como
/// lista de strings e não como objetos aninhados, porque a busca só precisa casar e
/// filtrar pelo nome — o detalhe completo continua vindo do banco relacional.
/// </summary>
public class GameSearchDocument
{
    [JsonPropertyName("gameId")]
    public int GameId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("developer")]
    public string? Developer { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("releaseDate")]
    public DateTime ReleaseDate { get; set; }

    [JsonPropertyName("basePrice")]
    public decimal BasePrice { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();

    public static GameSearchDocument FromEntity(Game game) => new()
    {
        GameId = game.GameId,
        Title = game.Title,
        Description = game.Description,
        Developer = game.Developer,
        Publisher = game.Publisher,
        ReleaseDate = game.ReleaseDate,
        BasePrice = game.BasePrice,
        IsActive = game.IsActive,
        Genres = game.GameGenres
            .Where(gg => gg.Genre is not null)
            .Select(gg => gg.Genre!.Name)
            .ToList()
    };
}
