using FIAP.CatalogAPI.Domain.Entities;

namespace FIAP.CatalogAPI.Domain.Interfaces;

public interface IGameRepository
{
    Task<Game?> GetByIdAsync(int gameId);
    Task<List<Game>> GetAllAsync();
    Task<Game> AddAsync(Game game);
    Task<Game> UpdateAsync(Game game);
    Task DeleteAsync(int gameId);
}
