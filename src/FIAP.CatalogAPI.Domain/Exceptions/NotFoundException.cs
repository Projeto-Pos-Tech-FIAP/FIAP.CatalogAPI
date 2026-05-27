namespace FIAP.CatalogAPI.Domain.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string entityName, object id)
        : base($"{entityName} com id '{id}' não encontrado.") { }
}
