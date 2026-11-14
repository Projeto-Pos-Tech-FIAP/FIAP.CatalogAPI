using FIAP.CatalogAPI.Api.Controllers;
using FIAP.CatalogAPI.Application.DTOs;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Domain.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FIAP.CatalogAPI.Tests.Features.Search;

public class SearchControllerTests
{
    private readonly Mock<IGameService> _gameServiceMock = new();
    private readonly SearchController _sut;

    public SearchControllerTests() => _sut = new SearchController(_gameServiceMock.Object);

    [Fact]
    public async Task SearchAsync_ShouldReturn200_WithHitsOrderedByRelevance()
    {
        var response = new GameSearchResponseDto
        {
            Query = "witcher",
            Total = 2,
            Page = 1,
            PageSize = 20,
            TookMs = 4,
            Items =
            [
                new GameSearchHitDto { GameId = 1, Title = "The Witcher 3", Score = 9.1 },
                new GameSearchHitDto { GameId = 2, Title = "The Witcher 2", Score = 5.3 }
            ]
        };

        _gameServiceMock
            .Setup(s => s.SearchAsync(It.IsAny<GameSearchQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _sut.SearchAsync(new GameSearchQueryDto { Q = "witcher" });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);

        var payload = ok.Value.Should().BeOfType<GameSearchResponseDto>().Subject;
        payload.Total.Should().Be(2);
        payload.Items.Select(i => i.Score).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task SearchAsync_ShouldReturn503_WhenSearchEngineIsUnavailable()
    {
        _gameServiceMock
            .Setup(s => s.SearchAsync(It.IsAny<GameSearchQueryDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SearchUnavailableException("O serviço de busca está indisponível."));

        var result = await _sut.SearchAsync(new GameSearchQueryDto { Q = "doom" });

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        objectResult.Value.Should().BeOfType<ExceptionOutputDto>()
            .Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task SearchAsync_ShouldForwardFilters_ToTheSearchEngine()
    {
        GameSearchQueryDto? captured = null;

        _gameServiceMock
            .Setup(s => s.SearchAsync(It.IsAny<GameSearchQueryDto>(), It.IsAny<CancellationToken>()))
            .Callback<GameSearchQueryDto, CancellationToken>((q, _) => captured = q)
            .ReturnsAsync(new GameSearchResponseDto());

        var query = new GameSearchQueryDto
        {
            Q = "rpg",
            Genre = "RPG",
            MinPrice = 50,
            MaxPrice = 200,
            OnlyActive = false,
            Page = 2,
            PageSize = 5
        };

        await _sut.SearchAsync(query);

        captured.Should().BeSameAs(query);
    }

    [Fact]
    public async Task ReindexAsync_ShouldReturn200_WithIndexedCount()
    {
        _gameServiceMock
            .Setup(s => s.ReindexSearchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(12);

        var result = await _sut.ReindexAsync();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().NotBeNull();
        ok.Value!.GetType().GetProperty("indexed")!.GetValue(ok.Value).Should().Be(12);
    }

    [Fact]
    public async Task ReindexAsync_ShouldReturn503_WhenSearchEngineIsUnavailable()
    {
        _gameServiceMock
            .Setup(s => s.ReindexSearchAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SearchUnavailableException("cluster fora"));

        var result = await _sut.ReindexAsync();

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }
}
