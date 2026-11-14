namespace FIAP.CatalogAPI.Domain.Exceptions;

/// <summary>
/// O cluster de busca não respondeu. Diferente do cache, a busca avançada não tem
/// equivalente no banco relacional (fuzzy e score de relevância), então a requisição
/// falha explicitamente em vez de devolver um resultado silenciosamente pior.
/// </summary>
public class SearchUnavailableException : Exception
{
    public SearchUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
