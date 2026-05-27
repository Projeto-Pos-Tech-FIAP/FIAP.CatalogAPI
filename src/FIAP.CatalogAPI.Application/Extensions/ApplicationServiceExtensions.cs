using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FIAP.CatalogAPI.Application.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        return services;
    }
}
