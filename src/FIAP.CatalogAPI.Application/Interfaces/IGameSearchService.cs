using FIAP.CatalogAPI.Application.DTOs;
using FIAP.CatalogAPI.Domain.Entities;

namespace FIAP.CatalogAPI.Application.Interfaces;

/// <summary>
/// Motor de busca do catálogo (Elasticsearch). É independente da consulta relacional:
/// a leitura vem do índice, não do SQL Server.
/// <para>
/// As operações de escrita (<see cref="IndexAsync"/>, <see cref="DeleteAsync"/>) degradam em
/// silêncio — se o cluster estiver fora, a gravação no banco principal não pode falhar por
/// isso. A reconciliação fica a cargo do <see cref="ReindexAllAsync"/>.
/// </para>
/// </summary>
public interface IGameSearchService
{
    /// <summary>
    /// Busca tolerante a erros de digitação (fuzzy), ordenada por relevância.
    /// </summary>
    Task<GameSearchResponseDto> SearchAsync(GameSearchQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>Cria ou substitui o documento do jogo no índice.</summary>
    Task IndexAsync(Game game, CancellationToken cancellationToken = default);

    /// <summary>Remove o jogo do índice (usado no soft delete).</summary>
    Task DeleteAsync(int gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recarrega o índice inteiro a partir da lista informada, em bulk. Serve para a carga
    /// inicial e para reconciliar o índice depois de uma indisponibilidade do cluster.
    /// </summary>
    /// <returns>Quantidade de documentos indexados.</returns>
    Task<int> ReindexAllAsync(IEnumerable<Game> games, CancellationToken cancellationToken = default);

    /// <summary>Garante que o índice existe com o mapping e os analisadores esperados.</summary>
    Task EnsureIndexAsync(CancellationToken cancellationToken = default);
}
