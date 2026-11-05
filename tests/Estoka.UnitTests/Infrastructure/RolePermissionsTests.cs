using Estoka.Application.Security;

namespace Estoka.UnitTests.Infrastructure;

public class RolePermissionsTests
{
    [Fact]
    public void Vendedor_nao_ve_custo_nem_gerencia_catalogo()
    {
        string[] roles = [Roles.Vendedor];
        RolePermissions.Has(roles, Permission.ViewCost).ShouldBeFalse();
        RolePermissions.Has(roles, Permission.ManageCatalog).ShouldBeFalse();
        RolePermissions.Has(roles, Permission.ManageOrders).ShouldBeTrue();
        RolePermissions.Has(roles, Permission.ViewStock).ShouldBeTrue();
        RolePermissions.Has(roles, Permission.GrantUnlimitedDiscount).ShouldBeFalse();
        RolePermissions.Has([Roles.Api], Permission.GrantUnlimitedDiscount).ShouldBeFalse();
    }

    [Fact]
    public void Gerente_tem_tudo_menos_usuarios()
    {
        foreach (var p in Enum.GetValues<Permission>())
            RolePermissions.Has([Roles.Gerente], p).ShouldBe(p != Permission.ManageUsers);
    }

    [Fact]
    public void Admin_tem_todas_as_permissoes()
        => Enum.GetValues<Permission>().ShouldAllBe(p => RolePermissions.Has(new[] { Roles.Admin }, p));
}
