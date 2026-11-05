using System.Text.Json;
using Estoka.Application.Common;
using Estoka.Application.Security;
using Estoka.Application.Users;
using Estoka.Domain.Auditing;
using Estoka.Domain.Common;
using Estoka.Infrastructure.Persistence;
using Estoka.Infrastructure.Persistence.Interceptors;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Estoka.Infrastructure.Identity;

/// <summary>
/// Gestão de usuários sobre o Identity. Cada operação usa um escopo próprio (UserManager + DbContext novos),
/// adequado ao Blazor Server; alterações de papel/ativação trocam o security stamp para derrubar sessões abertas.
/// </summary>
internal sealed class UserAdminService(
    IServiceScopeFactory scopeFactory, ICurrentUser currentUser, IValidator<CreateUserInput> validator, TimeProvider clock)
    : IUserAdminService
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default)
    {
        currentUser.Demand(Permission.ManageUsers);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id select new { ur.UserId, r.Name })
            .ToDictionaryAsync(x => x.UserId, x => x.Name!, ct);
        var users = await db.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync(ct);
        return users.Select(u => new UserDto(u.Id, u.FullName, u.Email ?? string.Empty, roles.GetValueOrDefault(u.Id, "—"), u.IsActive, u.CreatedAt))
            .ToList();
    }

    public async Task<string> CreateAsync(CreateUserInput input, CancellationToken ct = default)
    {
        currentUser.Demand(Permission.ManageUsers);
        await validator.EnsureValidAsync(input, ct);
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser
        {
            UserName = input.Email.Trim(), Email = input.Email.Trim(), EmailConfirmed = true, FullName = input.FullName.Trim(),
            IsActive = true, CreatedAt = clock.GetUtcNow(),
        };
        Check(await users.CreateAsync(user, input.Password));
        Check(await users.AddToRoleAsync(user, input.Role));
        await AuditAsync(scope, user.Id, AuditAction.Criado, null, new() { ["FullName"] = user.FullName, ["Email"] = user.Email, ["Role"] = input.Role }, ct);
        return user.Id;
    }

    public async Task ChangeRoleAsync(string userId, string role, CancellationToken ct = default)
    {
        currentUser.Demand(Permission.ManageUsers);
        if (!Roles.UserRoles.Contains(role)) throw new DomainException("Papel inválido.");
        if (userId == currentUser.UserId) throw new DomainException("Você não pode alterar o seu próprio papel.");
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByIdAsync(userId) ?? throw NotFoundException.For("Usuário");
        var current = await users.GetRolesAsync(user);
        if (current.Count == 1 && current[0] == role) return;
        Check(await users.RemoveFromRolesAsync(user, current));
        Check(await users.AddToRoleAsync(user, role));
        await users.UpdateSecurityStampAsync(user);
        await AuditAsync(scope, user.Id, AuditAction.Alterado, new() { ["Role"] = string.Join(", ", current) }, new() { ["Role"] = role }, ct);
    }

    public async Task SetActiveAsync(string userId, bool active, CancellationToken ct = default)
    {
        currentUser.Demand(Permission.ManageUsers);
        if (userId == currentUser.UserId && !active) throw new DomainException("Você não pode desativar o seu próprio usuário.");
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByIdAsync(userId) ?? throw NotFoundException.For("Usuário");
        if (user.IsActive == active) return;
        user.IsActive = active;
        Check(await users.UpdateAsync(user));
        await users.UpdateSecurityStampAsync(user); // encerra sessões abertas do usuário
        await AuditAsync(scope, user.Id, AuditAction.Alterado, new() { ["IsActive"] = !active }, new() { ["IsActive"] = active }, ct);
    }

    private async Task AuditAsync(AsyncServiceScope scope, string userId, AuditAction action,
        Dictionary<string, object?>? before, Dictionary<string, object?> after, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AuditLogs.Add(new AuditLog
        {
            EntityName = "Usuario",
            EntityId = (await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstAsync(ct)) ?? userId,
            Action = action,
            OldValues = before is null ? null : JsonSerializer.Serialize(before, AuditSaveChangesInterceptor.JsonOptions),
            NewValues = JsonSerializer.Serialize(after, AuditSaveChangesInterceptor.JsonOptions),
            UserName = currentUser.UserName,
            Timestamp = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new DomainException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
