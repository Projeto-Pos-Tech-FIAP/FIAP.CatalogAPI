using AutoMapper;
using FIAP.CatalogAPI.Application.DTOs;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Domain.Entities;
using FIAP.CatalogAPI.Domain.Exceptions;
using FIAP.CatalogAPI.Domain.Interfaces;

namespace FIAP.CatalogAPI.Application.Services;

public class GameService : IGameService
{
    private readonly IGameRepository _gameRepository;
    private readonly IGenreRepository _genreRepository;
    private readonly IMapper _mapper;

    public GameService(IGameRepository gameRepository, IGenreRepository genreRepository, IMapper mapper)
    {
        _gameRepository = gameRepository;
        _genreRepository = genreRepository;
        _mapper = mapper;
    }

    public async Task<GameOutputDto> CreateAsync(GameInputDto dto)
    {
        var game = new Game(
            dto.Title,
            dto.BasePrice,
            dto.CreatedBy,
            dto.Description,
            dto.Developer,
            dto.Publisher,
            dto.ReleaseDate,
            dto.IsActive);

        foreach (var genreId in dto.GenreIds)
        {
            var genre = await _genreRepository.GetByIdAsync(genreId)
                ?? throw new NotFoundException("Genre", genreId);

            game.GameGenres.Add(new GameGenre { GenreId = genreId });
        }

        var saved = await _gameRepository.AddAsync(game);
        return _mapper.Map<GameOutputDto>(saved);
    }

    public async Task<GameOutputDto> GetByIdAsync(int gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId)
            ?? throw new NotFoundException("Game", gameId);

        return _mapper.Map<GameOutputDto>(game);
    }

    public async Task<List<GameOutputDto>> GetAllAsync()
    {
        var games = await _gameRepository.GetAllAsync();
        return _mapper.Map<List<GameOutputDto>>(games);
    }

    public async Task<GameOutputDto> UpdateAsync(int gameId, GameUpdateDto dto)
    {
        var game = await _gameRepository.GetByIdAsync(gameId)
            ?? throw new NotFoundException("Game", gameId);

        game.Update(dto.Title, dto.BasePrice, dto.UpdatedBy, dto.Description,
            dto.Developer, dto.Publisher, dto.ReleaseDate, dto.IsActive);

        game.GameGenres.Clear();
        foreach (var genreId in dto.GenreIds)
        {
            var genre = await _genreRepository.GetByIdAsync(genreId)
                ?? throw new NotFoundException("Genre", genreId);

            game.GameGenres.Add(new GameGenre { GameId = gameId, GenreId = genreId });
        }

        var updated = await _gameRepository.UpdateAsync(game);
        return _mapper.Map<GameOutputDto>(updated);
    }

    public async Task DeleteAsync(int gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId)
            ?? throw new NotFoundException("Game", gameId);

        game.Delete();
        await _gameRepository.UpdateAsync(game);
    }
}
