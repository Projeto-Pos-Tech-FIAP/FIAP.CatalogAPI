using FIAP.CatalogAPI.Api.Controllers;
using FIAP.CatalogAPI.Application.DTOs;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Domain.Exceptions;
using FIAP.CatalogAPI.Tests.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FIAP.CatalogAPI.Tests.Features.Game;

public class GameControllerTests
{
    private readonly Mock<IGameService> _gameServiceMock;
    private readonly GameController _sut;

    public GameControllerTests()
    {
        _gameServiceMock = new Mock<IGameService>();
        _sut = new GameController(_gameServiceMock.Object);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturn200_WithGames()
    {
        var games = new List<GameOutputDto>
        {
            new() { GameId = 1, Title = "Game A", BasePrice = 10m },
            new() { GameId = 2, Title = "Game B", BasePrice = 20m }
        };

        _gameServiceMock.Setup(s => s.GetAllAsync()).ReturnsAsync(games);

        var result = await _sut.GetAllAsync();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);

        var returnedGames = okResult.Value.Should().BeAssignableTo<List<GameOutputDto>>().Subject;
        returnedGames.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturn200_WhenGameExists()
    {
        var game = new GameOutputDto { GameId = 1, Title = "Test Game", BasePrice = 59.99m };
        _gameServiceMock.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(game);

        var result = await _sut.GetByIdAsync(1);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturn404_WhenGameNotFound()
    {
        _gameServiceMock.Setup(s => s.GetByIdAsync(99))
            .ThrowsAsync(new NotFoundException("Game", 99));

        var result = await _sut.GetByIdAsync(99);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateAsync_ShouldReturn201_WhenValid()
    {
        var input = new GameInputDto { Title = "New Game", BasePrice = 49.99m, CreatedBy = Guid.NewGuid() };
        var output = new GameOutputDto { GameId = 10, Title = "New Game", BasePrice = 49.99m };

        _gameServiceMock.Setup(s => s.CreateAsync(input)).ReturnsAsync(output);

        var result = await _sut.CreateAsync(input);

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturn204_WhenGameExists()
    {
        _gameServiceMock.Setup(s => s.DeleteAsync(1)).Returns(Task.CompletedTask);

        var result = await _sut.DeleteAsync(1);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturn404_WhenGameNotFound()
    {
        _gameServiceMock.Setup(s => s.DeleteAsync(99))
            .ThrowsAsync(new NotFoundException("Game", 99));

        var result = await _sut.DeleteAsync(99);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
