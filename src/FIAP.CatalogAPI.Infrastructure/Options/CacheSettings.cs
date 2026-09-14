namespace FIAP.CatalogAPI.Infrastructure.Options;

public class CacheSettings
{
    public const string SectionName = "Cache";

    /// <summary>Prefixo aplicado a todas as chaves, para isolar o serviço na instância Redis.</summary>
    public string InstanceName { get; set; } = "catalog:";

    /// <summary>TTL padrão das entradas de cache, em segundos.</summary>
    public int DefaultTtlSeconds { get; set; } = 600;
}
