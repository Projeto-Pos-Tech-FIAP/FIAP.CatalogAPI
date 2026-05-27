using FIAP.CatalogAPI.Domain.Entities;
using FIAP.CatalogAPI.Domain.Interfaces;
using FIAP.CatalogAPI.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace FIAP.CatalogAPI.Infrastructure.Data.Repositories;

public class GameRepository : IGameRepository
{
    private readonly CatalogDbContext _context;

    public GameRepository(CatalogDbContext context) => _context = context;

    public async Task<Game?> GetByIdAsync(int gameId)
        => await _context.Games
            .Include(g => g.GameGenres)
            .ThenInclude(gg => gg.Genre)
            .FirstOrDefaultAsync(g => g.GameId == gameId);

    public async Task<List<Game>> GetAllAsync()
        => await _context.Games
            .Include(g => g.GameGenres)
            .ThenInclude(gg => gg.Genre)
            .AsNoTracking()
            .ToListAsync();

    public async Task<Game> AddAsync(Game game)
    {
        await _context.Games.AddAsync(game);
        await _context.SaveChangesAsync();
        return game;
    }

    public async Task<Game> UpdateAsync(Game game)
    {
        _context.Games.Update(game);
        await _context.SaveChangesAsync();
        return game;
    }

    public async Task DeleteAsync(int gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game is null) return;
        game.Delete();
        await _context.SaveChangesAsync();
    }
}
