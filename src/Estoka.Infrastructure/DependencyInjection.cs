using Estoka.Application.Abstractions;
using Estoka.Application.Security;
using Estoka.Infrastructure.Identity;
using Estoka.Infrastructure.Persistence;
using Estoka.Infrastructure.Persistence.Interceptors;
using Estoka.Infrastructure.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Estoka.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var raw = configuration.GetConnectionString("Default")
                  ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:Default' não configurada.");
        var requireSsl = configuration.GetValue("Database:RequireSsl", environment.IsProduction());
        var connectionString = ConnectionStringHelper.Normalize(raw, requireSsl);

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<AuditSaveChangesInterceptor>();

        // Fábrica com tempo de vida Scoped: cada operação cria seu DbContext, mas o interceptor
        // é resolvido no escopo atual (e enxerga o usuário logado). Também registra AppDbContext como Scoped.
        services.AddDbContextFactory<AppDbContext>((sp, options) =>
        {
            ConfigureDbContext(options, connectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
            if (environment.IsDevelopment()) options.EnableSensitiveDataLogging();
        }, ServiceLifetime.Scoped);
        services.AddScoped<IAppDbContextFactory, AppDbContextFactory>();

        services.AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 8;
                o.Password.RequireNonAlphanumeric = true;
                o.Lockout.MaxFailedAccessAttempts = 10;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<PortugueseIdentityErrorDescriber>();

        services.Configure<DemoOptions>(configuration.GetSection(DemoOptions.Section));
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<DemoDataSeeder>();
        services.AddHostedService<DemoResetBackgroundService>();

        return services;
    }

    internal static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString)
        => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            // Intencional: pedidos/movimentações de clientes e produtos excluídos (soft delete) usam IgnoreQueryFilters.
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
}
