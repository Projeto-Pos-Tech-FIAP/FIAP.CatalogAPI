using System.Text;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using FIAP.CatalogAPI.Application.DTOs;
using GameEntity = FIAP.CatalogAPI.Domain.Entities.Game;
using GameGenre = FIAP.CatalogAPI.Domain.Entities.GameGenre;
using Genre = FIAP.CatalogAPI.Domain.Entities.Genre;
using FIAP.CatalogAPI.Infrastructure.Options;
using FIAP.CatalogAPI.Infrastructure.Search;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FIAP.CatalogAPI.Tests.Features.Search;

/// <summary>
/// Exercita o serviço contra o transporte in-memory do cliente: as requisições são
/// realmente montadas e serializadas, mas nada sai da máquina. É o que permite afirmar
/// o que vai para o cluster sem depender de um cluster.
/// </summary>
public class ElasticGameSearchServiceTests
{
    private readonly List<string> _requests = new();

    private ElasticGameSearchService BuildService(string responseBody, int statusCode = 200)
    {
        var invoker = new InMemoryRequestInvoker(
            Encoding.UTF8.GetBytes(responseBody),
            statusCode,
            exception: null,
            contentType: "application/json",
            // Sem este header o cliente recusa a resposta por não reconhecer o produto.
            headers: new Dictionary<string, IEnumerable<string>>
            {
                ["x-elastic-product"] = ["Elasticsearch"]
            });

        var settings = new ElasticsearchClientSettings(invoker)
            .DisableDirectStreaming()
            .OnRequestCompleted(details =>
            {
                var body = details.RequestBodyInBytes is null
                    ? string.Empty
                    : Encoding.UTF8.GetString(details.RequestBodyInBytes);
                _requests.Add($"{details.HttpMethod} {details.Uri?.PathAndQuery}|{body}");
            });

        return new ElasticGameSearchService(
            new ElasticsearchClient(settings),
            Options.Create(new ElasticsearchSettings { GamesIndex = "games", Fuzziness = "AUTO" }),
            NullLogger<ElasticGameSearchService>.Instance);
    }

    private static GameEntity BuildGame(int gameId, string title)
    {
        var game = new GameEntity(title, 99.90m, Guid.NewGuid(), "descrição", "Dev", "Pub", new DateTime(2015, 5, 19));
        typeof(GameEntity).GetProperty(nameof(GameEntity.GameId))!.SetValue(game, gameId);
        game.GameGenres.Add(new GameGenre
        {
            GameId = gameId,
            GenreId = 1,
            Genre = new Genre { GenreId = 1, Name = "RPG" }
        });
        return game;
    }

    [Fact]
    public async Task SearchAsync_ShouldSendFuzzyQuerySortedByRelevance()
    {
        var sut = BuildService("""{"took":3,"timed_out":false,"hits":{"total":{"value":0,"relation":"eq"},"hits":[]}}""");

        await sut.SearchAsync(new GameSearchQueryDto { Q = "wticher" });

        var search = _requests.Single(r => r.Contains("/_search"));
        search.Should().Contain("\"fuzziness\":\"AUTO\"");
        search.Should().Contain("\"fuzzy_transpositions\":true");
        search.Should().Contain("title^4");
        search.Should().Contain("\"_score\":{\"order\":\"desc\"}");
        // Acerto exato tem que vencer o aproximado.
        search.Should().Contain("\"match_phrase\"");
    }

    [Fact]
    public async Task SearchAsync_ShouldSortAlphabetically_WhenThereIsNoTerm()
    {
        var sut = BuildService("""{"took":1,"timed_out":false,"hits":{"total":{"value":0,"relation":"eq"},"hits":[]}}""");

        await sut.SearchAsync(new GameSearchQueryDto());

        var search = _requests.Single(r => r.Contains("/_search"));
        search.Should().Contain("\"match_all\"");
        search.Should().Contain("\"title.keyword\":{\"order\":\"asc\"}");
        search.Should().NotContain("fuzziness");
    }

    [Fact]
    public async Task SearchAsync_ShouldApplyFiltersWithoutAffectingScore()
    {
        var sut = BuildService("""{"took":1,"timed_out":false,"hits":{"total":{"value":0,"relation":"eq"},"hits":[]}}""");

        await sut.SearchAsync(new GameSearchQueryDto { Q = "rpg", Genre = "RPG", MinPrice = 50, MaxPrice = 200 });

        var search = _requests.Single(r => r.Contains("/_search"));
        // Gênero e preço entram em "filter", que não pontua — não em "must".
        search.Should().Contain("\"filter\":[");
        search.Should().Contain("genres.keyword");
        search.Should().Contain("\"basePrice\":{\"gte\":50");
        search.Should().Contain("\"isActive\":{\"value\":true}");
    }

    [Fact]
    public async Task EnsureIndexAsync_ShouldCreateIndexWithExplicitMapping_WhenMissing()
    {
        // 404 no HEAD: o índice não existe.
        var sut = BuildService("{}", statusCode: 404);

        try { await sut.EnsureIndexAsync(); } catch { /* o PUT também responde 404 no stub */ }

        var create = _requests.Single(r => r.StartsWith("PUT /games|"));
        create.Should().Contain("catalog_analyzer");
        create.Should().Contain("asciifolding");
        create.Should().Contain("\"number_of_replicas\":0");
        // Sem mapping explícito o Elasticsearch inferiria float e long.
        create.Should().Contain("\"basePrice\":{\"type\":\"double\"}");
        create.Should().Contain("\"gameId\":{\"type\":\"integer\"}");
        // Subcampo keyword no título: é o que permite a ordenação alfabética.
        create.Should().Contain("\"keyword\":{\"ignore_above\":256");
    }

    [Fact]
    public async Task IndexAsync_ShouldWriteDocumentWithGenreNamesAndImmediateRefresh()
    {
        var sut = BuildService("""{"result":"created","_index":"games","_id":"42","_version":1}""");

        await sut.IndexAsync(BuildGame(42, "The Witcher 3"));

        var index = _requests.Single(r => r.StartsWith("PUT /games/_doc/42"));
        // refresh=true: o jogo recém-criado já aparece na busca seguinte.
        index.Should().Contain("refresh=true");
        // Nomes de gênero, não ids — é o que a busca precisa casar.
        index.Should().Contain("\"genres\":[\"RPG\"]");
    }

    [Fact]
    public async Task ReindexAllAsync_ShouldRecheckIndexExistence_EvenAfterItWasAlreadyReady()
    {
        var sut = BuildService("""{"took":5,"errors":false,"items":[]}""");

        // Primeiro o processo constata que o índice existe e guarda isso em cache.
        await sut.EnsureIndexAsync();

        await sut.ReindexAllAsync([BuildGame(1, "Jogo")]);

        // Regressão: sem a reverificação, um índice perdido depois deste ponto seria
        // recriado pelo _bulk com mapping dinâmico — sem o catalog_analyzer, com
        // basePrice como float e gameId como long.
        _requests.Count(r => r.StartsWith("HEAD /games")).Should().Be(2);
    }

    [Fact]
    public async Task ReindexAllAsync_ShouldSendOneBulkRequestWithExplicitIds()
    {
        var sut = BuildService("""{"took":5,"errors":false,"items":[]}""");

        var indexed = await sut.ReindexAllAsync([BuildGame(1, "Jogo A"), BuildGame(2, "Jogo B")]);

        indexed.Should().Be(2);
        var bulk = _requests.Single(r => r.Contains("/_bulk"));
        bulk.Should().Contain("refresh=true");
        bulk.Should().Contain("""{"index":{"_id":"1"}}""");
        bulk.Should().Contain("""{"index":{"_id":"2"}}""");
    }

    [Fact]
    public async Task ReindexAllAsync_ShouldDeleteDocumentsThatNoLongerExistInTheDatabase()
    {
        var sut = BuildService("""{"took":5,"errors":false,"items":[],"deleted":1,"total":1}""");

        await sut.ReindexAllAsync([BuildGame(1, "Jogo A"), BuildGame(2, "Jogo B")]);

        // Regressão: sem esta limpeza o _bulk apenas sobrescreve os ids que ainda existem,
        // e um jogo deletado enquanto o cluster estava fora sobrevive no índice como
        // fantasma e volta nas buscas.
        var purge = _requests.Single(r => r.Contains("/_delete_by_query"));
        purge.Should().Contain("\"must_not\"");
        purge.Should().Contain("\"gameId\"");
        purge.Should().Contain("[1,2]");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveDocumentById()
    {
        var sut = BuildService("""{"result":"deleted","_index":"games","_id":"42","_version":2}""");

        await sut.DeleteAsync(42);

        _requests.Should().ContainSingle(r => r.StartsWith("DELETE /games/_doc/42?refresh=true"));
    }
}
