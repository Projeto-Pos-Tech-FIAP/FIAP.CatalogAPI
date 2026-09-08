namespace FIAP.CatalogAPI.Application.Interfaces;

/// <summary>
/// Chaves de cache do catálogo. Todas sob o mesmo prefixo para que uma escrita em jogos
/// possa invalidar lista e detalhes de uma vez só.
/// </summary>
public static class CacheKeys
{
    public const string GamesPrefix = "games:";

    public const string AllGames = GamesPrefix + "all";

    public static string Game(int gameId) => $"{GamesPrefix}{gameId}";
}
