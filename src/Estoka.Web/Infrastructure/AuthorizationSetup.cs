using System.Security.Claims;
using Estoka.Application.Security;

namespace Estoka.Web.Infrastructure;

internal static class AuthorizationSetup
{
    /// <summary>Uma policy por permissão, derivada da matriz <see cref="RolePermissions"/> (mesma usada pelos serviços).</summary>
    public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
    {
        var builder = services.AddAuthorizationBuilder();
        foreach (var permission in Enum.GetValues<Permission>())
        {
            builder.AddPolicy(RolePermissions.PolicyName(permission), policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => RolePermissions.Has(ctx.User.FindAll(ClaimTypes.Role).Select(c => c.Value), permission)));
        }
        return services;
    }
}

/// <summary>Nomes das policies como constantes, para uso em <c>[Authorize(Policy = ...)]</c>.</summary>
public static class Policies
{
    public const string ViewCatalog = "perm:ViewCatalog";
    public const string ManageCatalog = "perm:ManageCatalog";
    public const string ViewCost = "perm:ViewCost";
    public const string ViewStock = "perm:ViewStock";
    public const string ManageStock = "perm:ManageStock";
    public const string ViewCustomers = "perm:ViewCustomers";
    public const string ViewOrders = "perm:ViewOrders";
    public const string ManageOrders = "perm:ManageOrders";
    public const string ViewDashboard = "perm:ViewDashboard";
    public const string ViewReports = "perm:ViewReports";
    public const string ViewAudit = "perm:ViewAudit";
    public const string ManageUsers = "perm:ManageUsers";
}
