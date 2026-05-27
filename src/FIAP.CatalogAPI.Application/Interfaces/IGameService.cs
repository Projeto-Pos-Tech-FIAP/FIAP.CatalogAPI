using FIAP.CatalogAPI.Application.DTOs;

namespace FIAP.CatalogAPI.Application.Interfaces;

public interface IGameService
{
    Task<GameOutputDto> CreateAsync(GameInputDto dto);
    Task<GameOutputDto> GetByIdAsync(int gameId);
    Task<List<GameOutputDto>> GetAllAsync();
    Task<GameOutputDto> UpdateAsync(int gameId, GameUpdateDto dto);
    Task DeleteAsync(int gameId);
}
