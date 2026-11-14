namespace FIAP.CatalogAPI.Application.DTOs;

/// <summary>Resultado paginado da busca avançada, já ordenado por relevância.</summary>
public class GameSearchResponseDto
{
    /// <summary>Termo efetivamente pesquisado.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Total de documentos que casaram com a busca no índice.</summary>
    public long Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    /// <summary>Tempo gasto pelo Elasticsearch para responder, em milissegundos.</summary>
    public long TookMs { get; set; }

    public List<GameSearchHitDto> Items { get; set; } = new();
}
