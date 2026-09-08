using System.Text.Json;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Infrastructure.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FIAP.CatalogAPI.Infrastructure.Caching;

/// <summary>
/// Cache distribuído sobre Redis. As leituras e escritas passam por
/// <see cref="IDistributedCache"/> (abstração do ASP.NET Core); a invalidação por prefixo
/// usa o <see cref="IConnectionMultiplexer"/> do StackExchange.Redis, porque varrer chaves
/// (SCAN) não existe na abstração.
/// </summary>
public class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly CacheSettings _settings;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IDistributedCache cache,
        IConnectionMultiplexer multiplexer,
        IOptions<CacheSettings> settings,
        ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _multiplexer = multiplexer;
        _settings = settings.Value;
        _logger = logger;
    }

    // Sem esta guarda, cada chamada com o Redis fora do ar espera o connect timeout
    // (segundos) antes de cair no catch — a "degradação" custaria mais que a consulta
    // que o cache deveria evitar. IsConnected é uma leitura local do multiplexer.
    private bool IsAvailable => _multiplexer.IsConnected;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (!IsAvailable) return null;

        try
        {
            var cached = await _cache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(cached))
            {
                _logger.LogDebug("Cache MISS | {Key}", key);
                return null;
            }

            _logger.LogDebug("Cache HIT | {Key}", key);
            return JsonSerializer.Deserialize<T>(cached, SerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler do Redis | {Key}. Seguindo sem cache.", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class
    {
        if (!IsAvailable) return;

        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromSeconds(_settings.DefaultTtlSeconds)
            };

            await _cache.SetStringAsync(key, JsonSerializer.Serialize(value, SerializerOptions), options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gravar no Redis | {Key}. Seguindo sem cache.", key);
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null) return cached;

        var value = await factory();
        await SetAsync(key, value, ttl, cancellationToken);

        return value;
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) return;

        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao remover chave do Redis | {Key}", key);
        }
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) return Task.CompletedTask;

        try
        {
            // IDistributedCache aplica InstanceName como prefixo físico das chaves; o SCAN
            // trabalha na chave real, então o prefixo precisa ser reconstruído aqui.
            var pattern = $"{_settings.InstanceName}{prefix}*";

            foreach (var endpoint in _multiplexer.GetEndPoints())
            {
                var server = _multiplexer.GetServer(endpoint);
                if (!server.IsConnected || server.IsReplica) continue;

                var database = _multiplexer.GetDatabase();

                foreach (var key in server.Keys(database.Database, pattern))
                    database.KeyDelete(key, CommandFlags.FireAndForget);
            }

            _logger.LogDebug("Cache invalidado por prefixo | {Pattern}", pattern);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao invalidar cache por prefixo | {Prefix}", prefix);
        }

        return Task.CompletedTask;
    }
}
