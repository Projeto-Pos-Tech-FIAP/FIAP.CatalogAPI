using FIAP.CatalogAPI.Domain.Interfaces;

namespace FIAP.CatalogAPI.Domain.Extensions;

public abstract class SoftDelete : ISoftDelete
{
    public bool IsDeleted { get; private set; }

    public void Delete() => IsDeleted = true;

    public void Restore() => IsDeleted = false;
}
