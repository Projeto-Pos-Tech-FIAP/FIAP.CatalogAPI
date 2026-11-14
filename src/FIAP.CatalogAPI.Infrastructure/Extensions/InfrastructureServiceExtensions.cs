using FIAP.CatalogAPI.Application.Extensions;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Application.Mappings;
using FIAP.CatalogAPI.Domain.Interfaces;
using FIAP.CatalogAPI.Infrastructure.Caching;
using FIAP.CatalogAPI.Infrastructure.Data.Context;
using FIAP.CatalogAPI.Infrastructure.Data.Mongo;
using FIAP.CatalogAPI.Infrastructure.Data.Repositories;
using FIAP.CatalogAPI.Infrastructure.Data.Services;
using FIAP.CatalogAPI.Infrastructure.Kafka;
using FIAP.CatalogAPI.Infrastructure.Options;
using FIAP.CatalogAPI.Infrastructure.Search;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace FIAP.CatalogAPI.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EF Core – SQL Server
        services.AddDbContext<CatalogDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Repositories
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IGenreRepository, GenreRepository>();
        services.AddScoped<ILibraryRepository, LibraryRepository>();

        services.AddMongoDb(configuration);
        services.AddRedisCache(configuration);
        services.AddElasticsearch(configuration);

        // Kafka
        services.Configure<KafkaSettings>(options =>
            configuration.GetSection(KafkaSettings.SectionName).Bind(options));
        services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
        services.AddHostedService<PaymentProcessedConsumer>();

        // AutoMapper
        services.AddAutoMapper(typeof(MappingProfile).Assembly);

        return services;
    }

    /// <summary>
    /// Persistência poliglota: MongoDB para auditoria de entidades e para os logs de
    /// eventos de integração (dados semiestruturados e de alta volumetria).
    /// </summary>
    private static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(options =>
            configuration.GetSection(MongoDbSettings.SectionName).Bind(options));

        // Singleton: o MongoClient já gerencia seu próprio pool de conexões.
        services.AddSingleton<MongoContext>();
        services.AddSingleton<IAuditService, MongoAuditService>();
        services.AddSingleton<IEventLogService, MongoEventLogService>();

        return services;
    }

    /// <summary>
    /// Busca avançada em Elasticsearch. O endereço e as credenciais vêm de configuração,
    /// então a mesma imagem serve para o cluster local, para o serviço no Kubernetes e
    /// para um endpoint gerenciado (Elastic Cloud / Amazon OpenSearch).
    /// </summary>
    private static IServiceCollection AddElasticsearch(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ElasticsearchSettings>(options =>
            configuration.GetSection(ElasticsearchSettings.SectionName).Bind(options));

        var settings = configuration.GetSection(ElasticsearchSettings.SectionName).Get<ElasticsearchSettings>()
            ?? new ElasticsearchSettings();

        // Singleton: o ElasticsearchClient é thread-safe e mantém o próprio pool de conexões
        // — instanciá-lo por requisição esgotaria sockets.
        services.AddSingleton(_ => BuildClient(settings));

        services.AddSingleton<IGameSearchService, ElasticGameSearchService>();
        services.AddHostedService<SearchIndexInitializer>();

        return services;
    }

    private static ElasticsearchClient BuildClient(ElasticsearchSettings settings)
    {
        var clientSettings = new ElasticsearchClientSettings(new Uri(settings.Uri))
            .DefaultIndex(settings.GamesIndex)
            // Timeout curto: a busca é um caminho de leitura do usuário, não pode ficar
            // pendurada esperando um cluster fora do ar.
            .RequestTimeout(TimeSpan.FromSeconds(settings.RequestTimeoutSeconds));

        // API Key tem precedência: é o formato usado pelo Elastic Cloud.
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            clientSettings = clientSettings.Authentication(new ApiKey(settings.ApiKey));
        else if (!string.IsNullOrWhiteSpace(settings.Username))
            clientSettings = clientSettings.Authentication(
                new BasicAuthentication(settings.Username, settings.Password ?? string.Empty));

        if (settings.AllowInvalidCertificate)
            clientSettings = clientSettings.ServerCertificateValidationCallback((_, _, _, _) => true);

        return new ElasticsearchClient(clientSettings);
    }

    /// <summary>
    /// Cache distribuído em Redis, exposto pela abstração IDistributedCache do ASP.NET Core
    /// e, para invalidação por prefixo, pelo IConnectionMultiplexer do StackExchange.Redis.
    /// </summary>
    private static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CacheSettings>(options =>
            configuration.GetSection(CacheSettings.SectionName).Bind(options));

        var cacheSettings = configuration.GetSection(CacheSettings.SectionName).Get<CacheSettings>()
            ?? new CacheSettings();

        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
        // Sem isso a aplicação não sobe se o Redis ainda não estiver pronto; com isso
        // o multiplexer reconecta em background e o cache degrada para "sempre miss".
        redisOptions.AbortOnConnectFail = false;
        // Timeouts curtos: com o Redis fora, o custo de descobrir isso não pode superar
        // o da consulta que o cache existe para evitar.
        redisOptions.ConnectTimeout = 2000;
        redisOptions.SyncTimeout = 2000;
        redisOptions.AsyncTimeout = 2000;

        services.AddStackExchangeRedisCache(options =>
        {
            // A mesma configuração do multiplexer abaixo — senão o IDistributedCache abriria
            // sua própria conexão com os timeouts padrão.
            options.ConfigurationOptions = redisOptions;
            options.InstanceName = cacheSettings.InstanceName;
        });

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));

        services.AddSingleton<ICacheService, RedisCacheService>();

        return services;
    }
}
