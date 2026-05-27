using FIAP.CatalogAPI.Domain.Entities;

namespace FIAP.CatalogAPI.Domain.Interfaces;

public interface IGenreRepository
{
    Task<Genre?> GetByIdAsync(int genreId);
    Task<List<Genre>> GetAllAsync();
}
