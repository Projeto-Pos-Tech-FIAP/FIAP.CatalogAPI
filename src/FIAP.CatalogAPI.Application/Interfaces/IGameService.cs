using FIAP.CatalogAPI.Application.DTOs;

namespace FIAP.CatalogAPI.Application.Interfaces;

public interface IGameService
{
    Task<GameOutputDto> CreateAsync(GameInputDto dto);
    Task<GameOutputDto> GetByIdAsync(int gameId);
    Task<List<GameOutputDto>> GetAllAsync();
    Task<GameOutputDto> UpdateAsync(int gameId, GameUpdateDto dto);
    Task DeleteAsync(int gameId);

    /// <summary>
    /// Busca avançada no Elasticsearch: tolerante a erros de digitação e ordenada por
    /// relevância. Não passa pelo banco relacional.
    /// </summary>
    Task<GameSearchResponseDto> SearchAsync(GameSearchQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recarrega o índice de busca a partir do banco relacional (carga inicial e
    /// reconciliação após indisponibilidade do cluster).
    /// </summary>
    /// <returns>Quantidade de jogos indexados.</returns>
    Task<int> ReindexSearchAsync(CancellationToken cancellationToken = default);
}
