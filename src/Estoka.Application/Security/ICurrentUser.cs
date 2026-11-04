using Estoka.Application.Common;

namespace Estoka.Application.Security;

public interface ICurrentUser
{
    string? UserId { get; }
    /// <summary>Nome exibido nos registros (movimentações, auditoria).</summary>
    string UserName { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Roles { get; }
}

public static class CurrentUserExtensions
{
    public static bool Can(this ICurrentUser user, Permission permission)
        => user.IsAuthenticated && RolePermissions.Has(user.Roles, permission);

    /// <summary>Autorização na camada de serviço — não confiamos apenas na tela.</summary>
    public static void Demand(this ICurrentUser user, Permission permission)
    {
        if (!user.Can(permission)) throw new ForbiddenException();
    }
}
