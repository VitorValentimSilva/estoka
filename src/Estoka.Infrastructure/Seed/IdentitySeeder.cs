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
}
