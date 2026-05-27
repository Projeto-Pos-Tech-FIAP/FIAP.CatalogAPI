using FIAP.CatalogAPI.Domain.Entities;

namespace FIAP.CatalogAPI.Domain.Interfaces;

public interface ILibraryRepository
{
    Task<Library?> GetByUserGuidAsync(Guid userGuid);
    Task<Library> AddAsync(Library library);
    Task<LibraryGame> AddGameAsync(LibraryGame libraryGame);
    Task<bool> UserOwnsGameAsync(Guid userGuid, int gameId);
}
