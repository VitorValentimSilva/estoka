using Microsoft.AspNetCore.Identity;

namespace Estoka.Infrastructure.Identity;

public sealed class AppUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
