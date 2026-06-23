using FIAP.CatalogAPI.Application.Extensions;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Application.Mappings;
using FIAP.CatalogAPI.Domain.Interfaces;
using FIAP.CatalogAPI.Infrastructure.Data.Context;
using FIAP.CatalogAPI.Infrastructure.Data.Repositories;
using FIAP.CatalogAPI.Infrastructure.Data.Services;
using FIAP.CatalogAPI.Infrastructure.Identity;
using FIAP.CatalogAPI.Infrastructure.Kafka;
using FIAP.CatalogAPI.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        // MongoDB – Audit
        services.Configure<MongoDbSettings>(options =>
            configuration.GetSection(MongoDbSettings.SectionName).Bind(options));
        services.AddScoped<IAuditService, MongoAuditService>();

        // Kafka
        services.Configure<KafkaSettings>(options =>
            configuration.GetSection(KafkaSettings.SectionName).Bind(options));
        services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
        services.AddHostedService<PaymentProcessedConsumer>();

        // AutoMapper
        services.AddAutoMapper(typeof(MappingProfile).Assembly);

        // Keycloak
        services.Configure<KeycloakSettings>(options =>
            configuration.GetSection(KeycloakSettings.SectionName).Bind(options));
        services.AddHttpClient<IKeycloakService, KeycloakService>();

        return services;
    }
}
