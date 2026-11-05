using Estoka.Application.Security;
using Estoka.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Estoka.Infrastructure.Seed;

public sealed class IdentitySeeder(
    RoleManager<IdentityRole> roleManager, UserManager<AppUser> userManager, TimeProvider clock, ILogger<IdentitySeeder> logger)
{
    public async Task EnsureRolesAsync()
    {
        foreach (var role in Roles.UserRoles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
    }

    /// <summary>Garante os usuários de demonstração (ativos, com a senha e o papel padrão).</summary>
    public async Task EnsureDemoUsersAsync(string password)
    {
        await EnsureRolesAsync();
        foreach (var (email, fullName, role) in DemoUsers.All)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new AppUser
                {
                    UserName = email, Email = email, EmailConfirmed = true, FullName = fullName, IsActive = true,
                    CreatedAt = clock.GetUtcNow(),
                };
                var created = await userManager.CreateAsync(user, password);
                if (!created.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
                logger.LogInformation("Usuário de demonstração {Email} criado", email);
            }
            else
            {
                // A demo é pública: restaura o estado padrão caso alguém tenha mexido — só quando necessário,
                // para não trocar o security stamp (o que derrubaria as sessões abertas a cada reinício).
                if (!user.IsActive || user.FullName != fullName || user.LockoutEnd is not null)
                {
                    user.IsActive = true;
                    user.FullName = fullName;
                    user.LockoutEnd = null;
                    await userManager.UpdateAsync(user);
                }
                if (!await userManager.CheckPasswordAsync(user, password))
                {
                    var token = await userManager.GeneratePasswordResetTokenAsync(user);
                    await userManager.ResetPasswordAsync(user, token, password);
                }
            }

            var roles = await userManager.GetRolesAsync(user);
            if (roles.Count != 1 || roles[0] != role)
            {
                await userManager.RemoveFromRolesAsync(user, roles);
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }

    /// <summary>Remove usuários criados por visitantes na demonstração pública (mantém só as contas de demo).</summary>
    public async Task<int> RemoveNonDemoUsersAsync()
    {
        var keep = DemoUsers.All.Select(u => u.Email.ToUpperInvariant()).ToArray();
        var extra = userManager.Users.Where(u => u.NormalizedEmail == null || !keep.Contains(u.NormalizedEmail)).ToList();
        foreach (var user in extra)
        {
            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
                logger.LogWarning("Não foi possível remover o usuário {Email}: {Errors}", user.Email,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        return extra.Count;
    }
}
