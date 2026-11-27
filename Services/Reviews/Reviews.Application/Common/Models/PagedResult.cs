namespace Reviews.Application.Common.Models;

/// <summary>Сторінка результатів: пагінація виконується в MongoDB (skip/limit), totalCount — countDocuments.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, long TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
