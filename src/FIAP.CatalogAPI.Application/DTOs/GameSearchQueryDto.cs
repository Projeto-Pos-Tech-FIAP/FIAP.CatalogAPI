using System.ComponentModel.DataAnnotations;

namespace FIAP.CatalogAPI.Application.DTOs;

/// <summary>Parâmetros aceitos pelo endpoint de busca avançada.</summary>
public class GameSearchQueryDto
{
    /// <summary>Termo livre. Vazio devolve o catálogo inteiro ordenado por título.</summary>
    [MaxLength(200)]
    public string? Q { get; set; }

    /// <summary>Filtra por nome de gênero (match exato, case-insensitive).</summary>
    [MaxLength(100)]
    public string? Genre { get; set; }

    /// <summary>Preço mínimo (inclusivo).</summary>
    [Range(0, double.MaxValue)]
    public decimal? MinPrice { get; set; }

    /// <summary>Preço máximo (inclusivo).</summary>
    [Range(0, double.MaxValue)]
    public decimal? MaxPrice { get; set; }

    /// <summary>Quando true (padrão), esconde jogos inativos.</summary>
    public bool OnlyActive { get; set; } = true;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
