namespace FIAP.CatalogAPI.Application.Interfaces;

/// <summary>
/// Cache distribuído (Redis). Toda implementação deve degradar em silêncio: se o Redis
/// estiver fora, a aplicação continua respondendo, apenas indo até o banco principal.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Lê do cache; em caso de miss executa <paramref name="factory"/> e grava o resultado.</summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class;

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Invalida todas as chaves que começam com <paramref name="prefix"/>.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
