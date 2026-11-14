namespace FIAP.CatalogAPI.Application.DTOs;

/// <summary>
/// Um jogo retornado pela busca avançada. Diferente do <see cref="GameOutputDto"/>,
/// carrega o score de relevância calculado pelo Elasticsearch — é ele que define a
/// ordem dos resultados e o que permite demonstrar a busca tolerante a erros.
/// </summary>
public class GameSearchHitDto
{
    public int GameId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Developer { get; set; }
    public string? Publisher { get; set; }
    public DateTime ReleaseDate { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; }
    public List<string> Genres { get; set; } = new();

    /// <summary>Score de relevância atribuído pelo Elasticsearch (quanto maior, melhor o match).</summary>
    public double Score { get; set; }
}
