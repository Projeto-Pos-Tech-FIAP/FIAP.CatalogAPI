using FIAP.CatalogAPI.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FIAP.CatalogAPI.Infrastructure.Search;

/// <summary>
/// Cria o índice de jogos no start da aplicação. Roda em background e com retry porque,
/// em Kubernetes e no docker-compose, a API quase sempre sobe antes do Elasticsearch
/// ficar pronto — e a falta do índice não pode impedir a API de atender.
/// </summary>
public class SearchIndexInitializer : BackgroundService
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly IGameSearchService _searchService;
    private readonly ILogger<SearchIndexInitializer> _logger;

    public SearchIndexInitializer(IGameSearchService searchService, ILogger<SearchIndexInitializer> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await _searchService.EnsureIndexAsync(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Índice de busca não pôde ser preparado (tentativa {Attempt}/{MaxAttempts}).",
                    attempt, MaxAttempts);

                try
                {
                    await Task.Delay(RetryDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        _logger.LogError(
            "Elasticsearch indisponível após {MaxAttempts} tentativas. A busca responderá 503 até o cluster voltar.",
            MaxAttempts);
    }
}
