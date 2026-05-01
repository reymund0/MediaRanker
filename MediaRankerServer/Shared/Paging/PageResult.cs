namespace MediaRankerServer.Shared.Paging;

public sealed record PageResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);
