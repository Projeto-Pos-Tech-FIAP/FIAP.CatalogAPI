using FIAP.CatalogAPI.Domain.Entities;
using FIAP.CatalogAPI.Domain.Interfaces;
using FIAP.CatalogAPI.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace FIAP.CatalogAPI.Infrastructure.Data.Repositories;

public class GenreRepository : IGenreRepository
{
    private readonly CatalogDbContext _context;

    public GenreRepository(CatalogDbContext context) => _context = context;

    public async Task<Genre?> GetByIdAsync(int genreId)
        => await _context.Genres.FindAsync(genreId);

    public async Task<List<Genre>> GetAllAsync()
        => await _context.Genres.AsNoTracking().ToListAsync();
}
