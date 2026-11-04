namespace Estoka.Application.Security;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Gerente = "Gerente";
    public const string Vendedor = "Vendedor";
    /// <summary>Papel técnico atribuído às chamadas da API autenticadas por API key.</summary>
    public const string Api = "Api";

    public static readonly string[] UserRoles = [Admin, Gerente, Vendedor];
}

public enum Permission
{
    ViewCatalog,
    ManageCatalog,
    ViewCost,
    ViewStock,
    ManageStock,
    ViewCustomers,
    ManageCustomers,
    ViewOrders,
    ManageOrders,
    ViewDashboard,
    ViewReports,
    ViewAudit,
    ManageUsers,
}

/// <summary>Matriz papel → permissões. Fonte única usada pelas policies (tela/API) e pelos serviços.</summary>
public static class RolePermissions
{
    private static readonly Dictionary<string, HashSet<Permission>> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        [Roles.Admin] = [.. Enum.GetValues<Permission>()],
        [Roles.Gerente] = [.. Enum.GetValues<Permission>().Where(p => p != Permission.ManageUsers)],
        [Roles.Vendedor] =
        [
            Permission.ViewCatalog, Permission.ViewStock, Permission.ViewCustomers, Permission.ManageCustomers,
            Permission.ViewOrders, Permission.ManageOrders, Permission.ViewDashboard,
        ],
        [Roles.Api] =
        [
            Permission.ViewCatalog, Permission.ViewStock, Permission.ViewCustomers, Permission.ViewOrders, Permission.ManageOrders,
        ],
    };

    public static bool Has(IEnumerable<string> roles, Permission permission)
        => roles.Any(r => Map.TryGetValue(r, out var set) && set.Contains(permission));

    public static string PolicyName(Permission permission) => $"perm:{permission}";
}
