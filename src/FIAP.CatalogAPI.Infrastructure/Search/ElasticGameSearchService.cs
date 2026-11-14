using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Analysis;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FIAP.CatalogAPI.Application.DTOs;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Domain.Entities;
using FIAP.CatalogAPI.Domain.Exceptions;
using FIAP.CatalogAPI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FIAP.CatalogAPI.Infrastructure.Search;

/// <summary>
/// Busca avançada do catálogo em Elasticsearch, com tolerância a erros de digitação
/// (fuzzy) e ordenação por relevância (_score).
/// </summary>
public class ElasticGameSearchService : IGameSearchService
{
    /// <summary>
    /// Analisador do catálogo: <c>lowercase</c> + <c>asciifolding</c>, para que "fifa" case
    /// com "FIFA" e "sao paulo" com "São Paulo" — acento não é erro de digitação que o
    /// fuzzy deva gastar distância de edição para corrigir.
    /// </summary>
    private const string CatalogAnalyzer = "catalog_analyzer";

    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchSettings _settings;
    private readonly ILogger<ElasticGameSearchService> _logger;

    // Garante que o índice só é criado uma vez por processo, sem pagar um ExistsAsync
    // em cada escrita.
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexReady;

    public ElasticGameSearchService(
        ElasticsearchClient client,
        IOptions<ElasticsearchSettings> settings,
        ILogger<ElasticGameSearchService> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    private string Index => _settings.GamesIndex;

    // ─── Leitura ────────────────────────────────────────────────────────────────

    public async Task<GameSearchResponseDto> SearchAsync(
        GameSearchQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var term = query.Q?.Trim();
        var hasTerm = !string.IsNullOrWhiteSpace(term);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var request = new SearchRequest(Index)
        {
            From = (page - 1) * pageSize,
            Size = pageSize,
            Query = BuildQuery(term, hasTerm, query),
            // Sem termo não há relevância para ordenar: cai para ordem alfabética.
            Sort = hasTerm
                ? [new SortOptions { Score = new ScoreSort { Order = SortOrder.Desc } }]
                : [new SortOptions { Field = new FieldSort { Field = "title.keyword", Order = SortOrder.Asc } }],
            TrackTotalHits = new TrackHits(true)
        };

        try
        {
            var response = await _client.SearchAsync<GameSearchDocument>(request, cancellationToken);

            if (!response.IsValidResponse)
            {
                // Índice ainda não criado (nenhum jogo indexado) não é erro: é busca vazia.
                if (response.ApiCallDetails?.HttpStatusCode == 404)
                {
                    _logger.LogWarning("Índice '{Index}' não existe. Rode POST /api/search/reindex.", Index);
                    return Empty(term, page, pageSize);
                }

                throw new SearchUnavailableException($"Busca indisponível — {Describe(response)}");
            }

            return new GameSearchResponseDto
            {
                Query = term ?? string.Empty,
                Total = response.Total,
                Page = page,
                PageSize = pageSize,
                TookMs = response.Took,
                Items = response.Hits.Where(h => h.Source is not null).Select(ToHitDto).ToList()
            };
        }
        catch (SearchUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao consultar o Elasticsearch | termo: {Term}", term);
            throw new SearchUnavailableException("O serviço de busca está indisponível.", ex);
        }
    }

    /// <summary>
    /// <c>must</c> casa o termo e produz o score; <c>filter</c> recorta o resultado sem
    /// interferir na relevância (filtros não pontuam); <c>should</c> empurra o acerto
    /// exato para cima do acerto aproximado.
    /// </summary>
    private Query BuildQuery(string? term, bool hasTerm, GameSearchQueryDto query)
    {
        var must = new List<Query>();
        var should = new List<Query>();
        var filter = new List<Query>();

        if (hasTerm)
        {
            must.Add(new MultiMatchQuery
            {
                Query = term!,
                // Pesos: o título é o que o usuário digita; descrição só desempata.
                Fields = new[] { "title^4", "genres^2", "developer^2", "publisher^1.5", "description" },
                // Tolerância a erro de digitação: AUTO = 1 edição em termos curtos, 2 nos longos.
                Fuzziness = new Fuzziness(_settings.Fuzziness),
                // Cobre a troca de letras adjacentes ("Clal of Duty") com 1 só edição.
                FuzzyTranspositions = true,
                Operator = Operator.Or,
                Type = TextQueryType.BestFields
            });

            // Quem digitou o título certo (ou quase) tem que vir antes de quem só casou
            // uma palavra por fuzzy.
            should.Add(new MatchPhraseQuery { Field = "title", Query = term!, Boost = 6 });
        }
        else
        {
            must.Add(new MatchAllQuery());
        }

        if (query.OnlyActive)
            filter.Add(new TermQuery { Field = "isActive", Value = true });

        if (!string.IsNullOrWhiteSpace(query.Genre))
            filter.Add(new TermQuery { Field = "genres.keyword", Value = query.Genre.Trim(), CaseInsensitive = true });

        if (query.MinPrice.HasValue || query.MaxPrice.HasValue)
            filter.Add(new NumberRangeQuery
            {
                Field = "basePrice",
                Gte = query.MinPrice.HasValue ? (double)query.MinPrice.Value : null,
                Lte = query.MaxPrice.HasValue ? (double)query.MaxPrice.Value : null
            });

        return new BoolQuery
        {
            Must = must,
            Should = should.Count > 0 ? should : null,
            Filter = filter.Count > 0 ? filter : null
        };
    }

    // ─── Escrita / sincronização ───────────────────────────────────────────────

    public async Task IndexAsync(Game game, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureIndexAsync(cancellationToken);

            var document = GameSearchDocument.FromEntity(game);

            var response = await _client.IndexAsync(
                document,
                descriptor => descriptor
                    .Index(Index)
                    .Id(document.GameId.ToString())
                    // Refresh imediato: sem isso o jogo recém-criado só apareceria na busca
                    // depois do refresh periódico (1s). O volume de escrita do catálogo é
                    // baixo, então o custo é irrelevante e o comportamento fica previsível.
                    .Refresh(Refresh.True),
                cancellationToken);

            if (!response.IsValidResponse)
            {
                LogIndexFailure(document.GameId, response);
                return;
            }

            _logger.LogInformation("Jogo {GameId} indexado em '{Index}'.", document.GameId, Index);
        }
        catch (Exception ex)
        {
            // A gravação no banco principal já aconteceu: derrubar a requisição aqui
            // perderia a escrita. O índice é reconciliado por POST /api/search/reindex.
            _logger.LogError(ex,
                "Jogo {GameId} gravado no banco mas não indexado. Rode POST /api/search/reindex.",
                game.GameId);
        }
    }

    public async Task DeleteAsync(int gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.DeleteAsync<GameSearchDocument>(
                Index,
                gameId.ToString(),
                descriptor => descriptor.Refresh(Refresh.True),
                cancellationToken);

            // 404 significa que o documento já não estava no índice — o efeito desejado.
            if (!response.IsValidResponse && response.ApiCallDetails?.HttpStatusCode != 404)
                _logger.LogError(
                    "Jogo {GameId} removido do banco mas não do índice ({Motivo}). Rode POST /api/search/reindex.",
                    gameId, Describe(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Jogo {GameId} removido do banco mas não do índice. Rode POST /api/search/reindex.", gameId);
        }
    }

    public async Task<int> ReindexAllAsync(IEnumerable<Game> games, CancellationToken cancellationToken = default)
    {
        // forceCheck: o reindex é o caminho explícito de reconciliação e não pode confiar
        // no cache. Se o índice foi perdido (cluster recriado, índice apagado, PVC limpo)
        // enquanto o processo seguia no ar, o cache diria "pronto" e o _bulk abaixo deixaria
        // o Elasticsearch criar o índice por mapping dinâmico — sem o catalog_analyzer,
        // com basePrice como float e gameId como long. Verificar aqui custa um HEAD.
        await EnsureIndexAsync(forceCheck: true, cancellationToken);

        var documents = games.Select(GameSearchDocument.FromEntity).ToList();
        if (documents.Count == 0) return 0;

        var response = await _client.BulkAsync(descriptor => descriptor
            .Index(Index)
            .IndexMany(documents, (operation, document) => operation.Id(document.GameId.ToString()))
            .Refresh(Refresh.True),
            cancellationToken);

        if (!response.IsValidResponse)
            throw new SearchUnavailableException($"Falha no bulk de reindexação — {Describe(response)}");

        var indexed = documents.Count;

        if (response.Errors)
        {
            var failures = response.ItemsWithErrors.Count();
            _logger.LogWarning("Reindexação concluída com {Failures} falha(s) de {Total}.", failures, documents.Count);
            indexed -= failures;
        }

        await RemoveOrphansAsync(documents.Select(d => d.GameId).ToList(), cancellationToken);

        _logger.LogInformation("Reindexação concluída: {Total} jogo(s) em '{Index}'.", indexed, Index);
        return indexed;
    }

    /// <summary>
    /// Apaga do índice os documentos que já não existem no banco. Sem isso o reindex só
    /// reconciliaria inclusões e alterações: um jogo deletado enquanto o cluster estava
    /// fora sobreviveria no índice como fantasma e voltaria nas buscas, porque o
    /// <c>_bulk</c> acima só sobrescreve os ids que ainda existem.
    /// </summary>
    private async Task RemoveOrphansAsync(ICollection<int> liveGameIds, CancellationToken cancellationToken)
    {
        var request = new DeleteByQueryRequest(Index)
        {
            Refresh = true,
            Query = new BoolQuery
            {
                MustNot = [new TermsQuery
                {
                    Field = "gameId",
                    Terms = new TermsQueryField(liveGameIds.Select(id => FieldValue.Long(id)).ToArray())
                }]
            }
        };

        var response = await _client.DeleteByQueryAsync(request, cancellationToken);

        if (!response.IsValidResponse)
        {
            // O índice tem os jogos atuais corretos; só sobrou lixo. Não vale derrubar o
            // reindex por isso, mas o operador precisa saber que o fantasma continua lá.
            _logger.LogWarning("Reindexação não conseguiu limpar documentos órfãos ({Motivo}).", Describe(response));
            return;
        }

        if (response.Deleted is > 0)
            _logger.LogInformation("Reindexação removeu {Deleted} documento(s) órfão(s) do índice.", response.Deleted);
    }

    // ─── Índice ────────────────────────────────────────────────────────────────

    public Task EnsureIndexAsync(CancellationToken cancellationToken = default)
        => EnsureIndexAsync(forceCheck: false, cancellationToken);

    /// <param name="forceCheck">
    /// Ignora o cache e consulta o cluster. Usado pelo reindex, que precisa detectar um
    /// índice perdido depois que o processo já o considerou pronto.
    /// </param>
    private async Task EnsureIndexAsync(bool forceCheck, CancellationToken cancellationToken)
    {
        if (_indexReady && !forceCheck) return;

        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            if (_indexReady && !forceCheck) return;

            var exists = await _client.Indices.ExistsAsync(Index, cancellationToken);
            if (exists.Exists)
            {
                _indexReady = true;
                return;
            }

            // O índice sumiu depois de o processo tê-lo visto pronto.
            if (_indexReady)
            {
                _indexReady = false;
                _logger.LogWarning("Índice '{Index}' não existe mais no cluster. Recriando com o mapping.", Index);
            }

            var response = await _client.Indices.CreateAsync(BuildCreateIndexRequest(), cancellationToken);

            // Outra réplica pode ter criado o índice entre o Exists e o Create.
            if (!response.IsValidResponse && response.ApiCallDetails?.HttpStatusCode != 400)
                throw new SearchUnavailableException($"Não foi possível criar o índice '{Index}' — {Describe(response)}");

            _indexReady = true;
            _logger.LogInformation("Índice '{Index}' pronto.", Index);
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    /// Mapping explícito. Sem ele o Elasticsearch inferiria os tipos no primeiro documento
    /// e os campos de texto ficariam sem o analisador do catálogo — o fuzzy e o filtro por
    /// gênero dependem dos subcampos <c>.keyword</c> declarados aqui.
    /// </summary>
    private CreateIndexRequest BuildCreateIndexRequest() => new(Index)
    {
        Settings = new IndexSettings
        {
            NumberOfShards = 1,
            // Single-node (dev e cluster de estudo): com réplica o índice ficaria yellow
            // para sempre, sem ganho nenhum.
            NumberOfReplicas = 0,
            Analysis = new IndexSettingsAnalysis
            {
                Analyzers = new Analyzers
                {
                    {
                        CatalogAnalyzer,
                        new CustomAnalyzer("standard") { Filter = new[] { "lowercase", "asciifolding" } }
                    }
                }
            }
        },
        Mappings = new TypeMapping
        {
            Properties = new Properties
            {
                { "gameId", new IntegerNumberProperty() },
                {
                    "title", new TextProperty
                    {
                        Analyzer = CatalogAnalyzer,
                        // Subcampo keyword: usado na ordenação alfabética (texto analisado
                        // não é ordenável).
                        Fields = new Properties { { "keyword", new KeywordProperty { IgnoreAbove = 256 } } }
                    }
                },
                { "description", new TextProperty { Analyzer = CatalogAnalyzer } },
                { "developer", new TextProperty { Analyzer = CatalogAnalyzer } },
                { "publisher", new TextProperty { Analyzer = CatalogAnalyzer } },
                {
                    "genres", new TextProperty
                    {
                        Analyzer = CatalogAnalyzer,
                        // Subcampo keyword: filtro por gênero é match exato, não busca textual.
                        Fields = new Properties { { "keyword", new KeywordProperty() } }
                    }
                },
                { "releaseDate", new DateProperty() },
                { "basePrice", new DoubleNumberProperty() },
                { "isActive", new BooleanProperty() }
            }
        }
    };

    // ─── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Um <c>IsValidResponse == false</c> cobre dois casos muito diferentes: o cluster
    /// inalcançável (sem status HTTP, com exceção de transporte) e o Elasticsearch
    /// recusando a operação (com status). Distinguir os dois é o que diz ao operador se
    /// o problema é de rede ou de requisição.
    /// </summary>
    private static string Describe(Elastic.Transport.Products.Elasticsearch.ElasticsearchResponse response)
    {
        var status = response.ApiCallDetails?.HttpStatusCode;
        var transportError = response.ApiCallDetails?.OriginalException?.Message;

        return status is null or 0
            ? $"cluster inalcançável: {transportError ?? "sem resposta"}"
            : $"Elasticsearch respondeu HTTP {status}: {transportError ?? response.DebugInformation}";
    }

    private void LogIndexFailure(
        int gameId,
        Elastic.Transport.Products.Elasticsearch.ElasticsearchResponse response)
        => _logger.LogError(
            "Jogo {GameId} gravado no banco mas não indexado ({Motivo}). Rode POST /api/search/reindex.",
            gameId, Describe(response));

    private static GameSearchResponseDto Empty(string? term, int page, int pageSize) => new()
    {
        Query = term ?? string.Empty,
        Total = 0,
        Page = page,
        PageSize = pageSize,
        TookMs = 0
    };

    private static GameSearchHitDto ToHitDto(Elastic.Clients.Elasticsearch.Core.Search.Hit<GameSearchDocument> hit)
    {
        var source = hit.Source!;
        return new GameSearchHitDto
        {
            GameId = source.GameId,
            Title = source.Title,
            Description = source.Description,
            Developer = source.Developer,
            Publisher = source.Publisher,
            ReleaseDate = source.ReleaseDate,
            BasePrice = source.BasePrice,
            IsActive = source.IsActive,
            Genres = source.Genres,
            Score = hit.Score ?? 0
        };
    }
}
