namespace Estoka.Application.Common;

/// <summary>Página base zero, compatível com o MudDataGrid.</summary>
public record PageRequest
{
    public int Page { get; init; }
    public int PageSize { get; init; } = 25;
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }

    public int SafePageSize => Math.Clamp(PageSize, 1, 200);
    public int Skip => Math.Max(Page, 0) * SafePageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount)
{
    public static PagedResult<T> Empty { get; } = new([], 0);
}

public static class PagingExtensions
{
    public static IQueryable<T> Page<T>(this IQueryable<T> query, PageRequest request)
        => query.Skip(request.Skip).Take(request.SafePageSize);
}
