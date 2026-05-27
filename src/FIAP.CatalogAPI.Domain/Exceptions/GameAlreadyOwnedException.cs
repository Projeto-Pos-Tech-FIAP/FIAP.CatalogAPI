namespace FIAP.CatalogAPI.Domain.Exceptions;

public class GameAlreadyOwnedException : Exception
{
    public GameAlreadyOwnedException(Guid userId, int gameId)
        : base($"Usuário '{userId}' já possui o jogo '{gameId}' na biblioteca.") { }
}
