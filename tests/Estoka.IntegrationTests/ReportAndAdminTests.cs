using System.Net;
using Estoka.Application.Auditing;
using Estoka.Application.Common;
using Estoka.Application.Reports;
using Estoka.Application.Security;
using Estoka.Application.Users;
using Estoka.Domain.Common;
using Estoka.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Estoka.IntegrationTests;

[Collection(AppCollection.Name)]
public class ReportAndAdminTests(EstokaFactory factory)
{
    [Theory]
    [InlineData("vendas.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("curva-abc.pdf", "application/pdf")]
    [InlineData("estoque.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task Gerente_exporta_relatorios(string file, string contentType)
    {
        using var client = factory.CreateBrowserClient();
        await client.LoginAsync("gerente@estoka.app");
        var response = await client.GetAsync($"/relatorios/exportar/{file}", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(contentType);
        (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Length.ShouldBeGreaterThan(1000);
    }

    [Fact]
    public async Task Vendedor_nao_acessa_relatorio_com_custo_via_http()
    {
        using var client = factory.CreateBrowserClient();
        await client.LoginAsync("vendedor@estoka.app");
        var response = await client.GetAsync("/relatorios/exportar/estoque.xlsx", TestContext.Current.CancellationToken);
        response.IsRedirectTo("/acesso-negado").ShouldBeTrue($"veio {response.StatusCode} {response.Headers.Location}");
    }

    [Fact]
    public async Task Curva_abc_classifica_ate_80_95_100_por_cento()
    {
        await using var scope = factory.ScopeAs(Roles.Gerente);
        var service = scope.ServiceProvider.GetRequiredService<ReportService>();
        var report = await service.AbcAsync(service.DefaultPeriod() with { From = BrazilTime.Today(TimeProvider.System).AddDays(-89) }, TestContext.Current.CancellationToken);

        report.Rows.ShouldNotBeEmpty();
        report.Rows[^1].CumulativePercent.ShouldBeInRange(99.9m, 100.1m);
        report.Rows.Where(r => r.Class == 'A').ShouldAllBe(r => r.CumulativePercent - r.SharePercent < 80);
        report.Rows.Where(r => r.Class == 'C').ShouldAllBe(r => r.CumulativePercent - r.SharePercent >= 95);
        report.Rows.Select(r => r.Revenue).ShouldBeInOrder(SortDirection.Descending);
    }

    [Fact]
    public async Task Relatorio_de_vendas_bate_com_dashboard_e_oculta_custo_sem_permissao()
    {
        await using var scope = factory.ScopeAs(Roles.Gerente);
        var service = scope.ServiceProvider.GetRequiredService<ReportService>();
        var report = await service.SalesAsync(service.DefaultPeriod(), TestContext.Current.CancellationToken);
        report.Days.Count.ShouldBe(30);
        report.IncludesCost.ShouldBeTrue();
        report.Net.ShouldBe(report.Gross - report.Discounts);

        await using var seller = factory.ScopeAs(Roles.Vendedor);
        await Should.ThrowAsync<ForbiddenException>(() =>
            seller.ServiceProvider.GetRequiredService<ReportService>().StockPositionAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Admin_cria_usuario_muda_papel_e_desativa_com_auditoria()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = factory.ScopeAs(Roles.Admin, "Mariana");
        var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
        var email = $"novo{Guid.NewGuid():N}"[..12] + "@estoka.app";

        var id = await users.CreateAsync(new CreateUserInput { FullName = "Novo Vendedor", Email = email, Role = Roles.Vendedor, Password = "Senha@123" }, ct);
        await users.ChangeRoleAsync(id, Roles.Gerente, ct);
        await users.SetActiveAsync(id, false, ct);

        var created = (await users.ListAsync(ct)).Single(u => u.Id == id);
        created.Role.ShouldBe(Roles.Gerente);
        created.IsActive.ShouldBeFalse();

        using var client = factory.CreateBrowserClient();
        var login = await client.LoginAsync(email, "Senha@123");
        login.StatusCode.ShouldBe(HttpStatusCode.OK, "usuário desativado não entra");

        var audit = await scope.ServiceProvider.GetRequiredService<AuditService>().GetPagedAsync(new AuditQuery { EntityName = "Usuario", PageSize = 50 }, ct);
        audit.Items.Where(a => a.EntityId == email).Count().ShouldBe(3);
    }

    [Fact]
    public async Task Gerente_nao_gerencia_usuarios_e_admin_nao_se_desativa()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var manager = factory.ScopeAs(Roles.Gerente);
        await Should.ThrowAsync<ForbiddenException>(() => manager.ServiceProvider.GetRequiredService<IUserAdminService>().ListAsync(ct));

        await using var admin = factory.ScopeAs(Roles.Admin);
        var service = admin.ServiceProvider.GetRequiredService<IUserAdminService>();
        var me = admin.ServiceProvider.GetRequiredService<ICurrentUser>().UserId!;
        await Should.ThrowAsync<DomainException>(() => service.SetActiveAsync(me, false, ct));
    }
}
