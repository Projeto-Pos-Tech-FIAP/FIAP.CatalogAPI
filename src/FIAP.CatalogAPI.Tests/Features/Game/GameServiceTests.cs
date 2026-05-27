using AutoMapper;
using FIAP.CatalogAPI.Application.DTOs;
using FIAP.CatalogAPI.Application.Mappings;
using FIAP.CatalogAPI.Application.Services;
using FIAP.CatalogAPI.Domain.Exceptions;
using FIAP.CatalogAPI.Domain.Interfaces;
using FIAP.CatalogAPI.Tests.Builders;
using FluentAssertions;
using Moq;

namespace FIAP.CatalogAPI.Tests.Features.Game;

public class GameServiceTests
{
    private readonly Mock<IGameRepository> _gameRepositoryMock;
    private readonly Mock<IGenreRepository> _genreRepositoryMock;
    private readonly IMapper _mapper;
    private readonly GameService _sut;

    public GameServiceTests()
    {
        _gameRepositoryMock = new Mock<IGameRepository>();
        _genreRepositoryMock = new Mock<IGenreRepository>();

        _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();

        _sut = new GameService(_gameRepositoryMock.Object, _genreRepositoryMock.Object, _mapper);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnGameOutputDto_WhenValidInput()
    {
        var createdBy = Guid.NewGuid();
        var dto = new GameInputDto
        {
            Title = "New Game",
            BasePrice = 49.99m,
            CreatedBy = createdBy,
            Description = "A great game",
            IsActive = true
        };

        var game = GameBuilder.New().WithTitle(dto.Title).WithPrice(dto.BasePrice).WithCreatedBy(createdBy).Build();

        _gameRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Domain.Entities.Game>()))
            .ReturnsAsync(game);

        var result = await _sut.CreateAsync(dto);

        result.Should().NotBeNull();
        result.Title.Should().Be(dto.Title);
        result.BasePrice.Should().Be(dto.BasePrice);
        _gameRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Domain.Entities.Game>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnGameOutputDto_WhenGameExists()
    {
        var game = GameBuilder.New().WithGameId(1).WithTitle("Existing Game").Build();

        _gameRepositoryMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(game);

        var result = await _sut.GetByIdAsync(1);

        result.Should().NotBeNull();
        result.GameId.Should().Be(1);
        result.Title.Should().Be("Existing Game");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrowNotFoundException_WhenGameDoesNotExist()
    {
        _gameRepositoryMock.Setup(r => r.GetByIdAsync(99))
            .ReturnsAsync((Domain.Entities.Game?)null);

        var act = async () => await _sut.GetByIdAsync(99);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllGames()
    {
        var games = new List<Domain.Entities.Game>
        {
            GameBuilder.New().WithGameId(1).WithTitle("Game 1").Build(),
            GameBuilder.New().WithGameId(2).WithTitle("Game 2").Build(),
            GameBuilder.New().WithGameId(3).WithTitle("Game 3").Build()
        };

        _gameRepositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(games);

        var result = await _sut.GetAllAsync();

        result.Should().HaveCount(3);
        result.Select(g => g.Title).Should().BeEquivalentTo("Game 1", "Game 2", "Game 3");
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnUpdatedGame_WhenGameExists()
    {
        var updatedBy = Guid.NewGuid();
        var game = GameBuilder.New().WithGameId(5).WithTitle("Old Title").Build();
        var dto = new GameUpdateDto
        {
            Title = "New Title",
            BasePrice = 29.99m,
            UpdatedBy = updatedBy
        };

        _gameRepositoryMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(game);
        _gameRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Domain.Entities.Game>()))
            .ReturnsAsync((Domain.Entities.Game g) => g);

        var result = await _sut.UpdateAsync(5, dto);

        result.Title.Should().Be("New Title");
        result.BasePrice.Should().Be(29.99m);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowNotFoundException_WhenGameDoesNotExist()
    {
        _gameRepositoryMock.Setup(r => r.GetByIdAsync(99))
            .ReturnsAsync((Domain.Entities.Game?)null);

        var act = async () => await _sut.UpdateAsync(99, new GameUpdateDto { Title = "X", UpdatedBy = Guid.NewGuid() });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_ShouldSoftDeleteGame_WhenGameExists()
    {
        var game = GameBuilder.New().WithGameId(3).Build();

        _gameRepositoryMock.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(game);
        _gameRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<Domain.Entities.Game>()))
            .ReturnsAsync((Domain.Entities.Game g) => g);

        await _sut.DeleteAsync(3);

        game.IsDeleted.Should().BeTrue();
        _gameRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Domain.Entities.Game>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowNotFoundException_WhenGameDoesNotExist()
    {
        _gameRepositoryMock.Setup(r => r.GetByIdAsync(99))
            .ReturnsAsync((Domain.Entities.Game?)null);

        var act = async () => await _sut.DeleteAsync(99);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
