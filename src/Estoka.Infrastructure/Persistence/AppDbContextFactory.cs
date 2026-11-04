using Estoka.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Estoka.Infrastructure.Persistence;

internal sealed class AppDbContextFactory(IDbContextFactory<AppDbContext> inner) : IAppDbContextFactory
{
    public async Task<IAppDbContext> CreateAsync(CancellationToken cancellationToken = default)
        => await inner.CreateDbContextAsync(cancellationToken);
}
