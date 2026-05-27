using FIAP.CatalogAPI.Domain.Entities;
using FIAP.CatalogAPI.Domain.Interfaces;
using FIAP.CatalogAPI.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace FIAP.CatalogAPI.Infrastructure.Data.Repositories;

public class LibraryRepository : ILibraryRepository
{
    private readonly CatalogDbContext _context;

    public LibraryRepository(CatalogDbContext context) => _context = context;

    public async Task<Library?> GetByUserGuidAsync(Guid userGuid)
        => await _context.Libraries
            .Include(l => l.LibraryGames)
            .ThenInclude(lg => lg.Game)
            .FirstOrDefaultAsync(l => l.UserGuid == userGuid);

    public async Task<Library> AddAsync(Library library)
    {
        await _context.Libraries.AddAsync(library);
        await _context.SaveChangesAsync();
        return library;
    }

    public async Task<LibraryGame> AddGameAsync(LibraryGame libraryGame)
    {
        await _context.LibraryGames.AddAsync(libraryGame);
        await _context.SaveChangesAsync();
        return libraryGame;
    }

    public async Task<bool> UserOwnsGameAsync(Guid userGuid, int gameId)
        => await _context.Libraries
            .Where(l => l.UserGuid == userGuid)
            .SelectMany(l => l.LibraryGames)
            .AnyAsync(lg => lg.GameId == gameId);
}
