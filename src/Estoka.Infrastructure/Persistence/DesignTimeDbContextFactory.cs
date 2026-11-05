using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Estoka.Infrastructure.Persistence;

/// <summary>Usado apenas pelo <c>dotnet ef</c> para gerar migrations.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                 ?? "Host=localhost;Database=estoka;Username=estoka;Password=estoka";
        var options = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureDbContext(options, ConnectionStringHelper.Normalize(cs, requireSsl: false));
        return new AppDbContext(options.Options);
    }
}
