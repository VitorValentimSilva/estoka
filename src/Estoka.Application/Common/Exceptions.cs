namespace Estoka.Application.Common;

public sealed class NotFoundException(string message) : Exception(message)
{
    public static NotFoundException For(string entity) => new($"{entity} não encontrado(a).");
}

public sealed class ForbiddenException(string message = "Você não tem permissão para realizar esta operação.") : Exception(message);

public sealed class ConcurrencyConflictException(
    string message = "Este registro foi alterado por outro usuário enquanto você editava. Recarregue os dados e tente novamente.")
    : Exception(message);
