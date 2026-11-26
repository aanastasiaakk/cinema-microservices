namespace Catalog.Bll.Dto;

/// <summary>
/// Контракт відповіді колекцій. Метадані — у тілі відповіді (і дублюються заголовками X-Total-Count,
/// X-Total-Pages у контролері).
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}

/// <summary>Спільні межі пагінації.</summary>
public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public static int ClampPageSize(int pageSize) => Math.Clamp(pageSize, 1, MaxPageSize);
}
