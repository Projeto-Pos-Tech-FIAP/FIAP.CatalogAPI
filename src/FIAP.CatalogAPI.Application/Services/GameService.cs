using AutoMapper;
using FIAP.CatalogAPI.Application.DTOs;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Domain.Entities;
using FIAP.CatalogAPI.Domain.Exceptions;
using FIAP.CatalogAPI.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace FIAP.CatalogAPI.Application.Services;

public class GameService : IGameService
{
    private readonly IGameRepository _gameRepository;
    private readonly IGenreRepository _genreRepository;
    private readonly ICacheService _cache;
    private readonly IGameSearchService _search;
    private readonly IMapper _mapper;
    private readonly ILogger<GameService> _logger;

    public GameService(
        IGameRepository gameRepository,
        IGenreRepository genreRepository,
        ICacheService cache,
        IGameSearchService search,
        IMapper mapper,
        ILogger<GameService> logger)
    {
        _gameRepository = gameRepository;
        _genreRepository = genreRepository;
        _cache = cache;
        _search = search;
        _mapper = mapper;
        _logger = logger;
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

        await InvalidateGamesCacheAsync();
        await SyncSearchIndexAsync(saved.GameId);

        return _mapper.Map<GameOutputDto>(saved);
    }

    /// <summary>
    /// Consulta cacheada no Redis: o detalhe do jogo carrega gêneros por join e é o
    /// endpoint mais chamado do catálogo.
    /// </summary>
    public async Task<GameOutputDto> GetByIdAsync(int gameId)
    {
        var cached = await _cache.GetAsync<GameOutputDto>(CacheKeys.Game(gameId));
        if (cached is not null) return cached;

        var game = await _gameRepository.GetByIdAsync(gameId)
            ?? throw new NotFoundException("Game", gameId);

        var dto = _mapper.Map<GameOutputDto>(game);
        await _cache.SetAsync(CacheKeys.Game(gameId), dto);

        return dto;
    }

    /// <summary>
    /// Listagem completa do catálogo — a consulta mais cara do serviço e a que menos muda.
    /// </summary>
    public async Task<List<GameOutputDto>> GetAllAsync()
    {
        return await _cache.GetOrSetAsync(CacheKeys.AllGames, async () =>
        {
            var games = await _gameRepository.GetAllAsync();
            return _mapper.Map<List<GameOutputDto>>(games);
        });
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

        await InvalidateGamesCacheAsync();
        await SyncSearchIndexAsync(updated.GameId);

        return _mapper.Map<GameOutputDto>(updated);
    }

    public async Task DeleteAsync(int gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId)
            ?? throw new NotFoundException("Game", gameId);

        game.Delete();
        await _gameRepository.UpdateAsync(game);

        await InvalidateGamesCacheAsync();

        // Jogo com soft delete sai do índice: a busca não deve devolver o que a API
        // já considera removido.
        try
        {
            await _search.DeleteAsync(gameId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jogo {GameId} removido do banco mas não do índice de busca. "
                + "Rode POST /api/search/reindex.", gameId);
        }
    }

    public Task<GameSearchResponseDto> SearchAsync(
        GameSearchQueryDto query,
        CancellationToken cancellationToken = default)
        => _search.SearchAsync(query, cancellationToken);

    public async Task<int> ReindexSearchAsync(CancellationToken cancellationToken = default)
    {
        // Lê direto do repositório (e não de GetAllAsync) porque a indexação precisa das
        // entidades com os gêneros carregados, não do DTO de saída.
        var games = await _gameRepository.GetAllAsync();
        return await _search.ReindexAllAsync(games, cancellationToken);
    }

    /// <summary>
    /// Reindexa o jogo depois da escrita no banco principal. Relê a entidade para que o
    /// documento carregue os nomes dos gêneros — em um create/update o relacionamento
    /// gravado só tem os ids.
    /// </summary>
    private async Task SyncSearchIndexAsync(int gameId)
    {
        // A escrita no banco principal já foi confirmada. Propagar uma falha de indexação
        // daqui devolveria erro ao cliente para uma operação que, de fato, deu certo —
        // e o jogo ficaria criado sem que ninguém soubesse. O índice é reconciliado por
        // POST /api/search/reindex.
        try
        {
            var persisted = await _gameRepository.GetByIdAsync(gameId);
            if (persisted is null) return;

            await _search.IndexAsync(persisted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jogo {GameId} gravado no banco mas não indexado. "
                + "Rode POST /api/search/reindex.", gameId);
        }
    }

    // Escrita em qualquer jogo derruba lista e detalhes: manter a granularidade fina
    // custaria mais do que o TTL curto economiza.
    private Task InvalidateGamesCacheAsync() => _cache.RemoveByPrefixAsync(CacheKeys.GamesPrefix);
}
