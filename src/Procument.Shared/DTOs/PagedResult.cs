using Microsoft.EntityFrameworkCore;

namespace Procument.Shared.DTOs;

/// <summary>
/// Common pagination / search query parameters accepted by list endpoints.
/// Bind with <c>[FromQuery]</c>.
/// </summary>
public class PageQuery
{
    private int _page = 1;
    private int _pageSize = 50;

    /// <summary>1-based page number. Values &lt; 1 are clamped to 1.</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Page size. Clamped to [1, 500].</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 1 : (value > 500 ? 500 : value);
    }

    /// <summary>Optional free-text search term — interpretation is up to each endpoint.</summary>
    public string? Search { get; set; }

    /// <summary>Optional sort token — interpretation is up to each endpoint.</summary>
    public string? Sort { get; set; }
}

/// <summary>
/// Paged response envelope returned by list endpoints.
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

/// <summary>
/// Extension helpers for turning an <see cref="IQueryable{T}"/> into a <see cref="PagedResult{T}"/>
/// with a single call (issues one COUNT query and one page query).
/// </summary>
public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 500) pageSize = 500;

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public static Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PageQuery page,
        CancellationToken cancellationToken = default)
        => query.ToPagedResultAsync(page.Page, page.PageSize, cancellationToken);
}
