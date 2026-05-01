namespace MediaRankerServer.Shared.Paging;

public sealed record PageRequest(
    int Page = 0,
    int PageSize = 25,
    string? SortField = null,
    string? SortDirection = "asc",
    string? SearchTerm = null,
    string? SearchField = null
);
