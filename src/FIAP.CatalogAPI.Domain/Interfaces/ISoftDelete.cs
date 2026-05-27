namespace FIAP.CatalogAPI.Domain.Interfaces;

public interface ISoftDelete
{
    bool IsDeleted { get; }
    void Delete();
    void Restore();
}
