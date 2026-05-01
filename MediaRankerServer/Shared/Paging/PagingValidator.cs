using MediaRankerServer.Shared.Exceptions;

namespace MediaRankerServer.Shared.Paging;

public static class PagingValidator
{
    public const int MaxPageSize = 100;

    public static PagingValidationResult Validate(
        PageRequest request,
        IReadOnlyCollection<string> allowedSortFields,
        IReadOnlyCollection<string> allowedSearchFields,
        string defaultSortField)
    {
        if (request.Page < 0)
            throw new DomainException("Page must be >= 0.", "paging_validation_error");

        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
            throw new DomainException($"PageSize must be between 1 and {MaxPageSize}.", "paging_validation_error");

        var sortField = string.IsNullOrWhiteSpace(request.SortField)
            ? defaultSortField
            : allowedSortFields.FirstOrDefault(f => string.Equals(f, request.SortField, StringComparison.OrdinalIgnoreCase))
              ?? throw new DomainException($"Sort field '{request.SortField}' is not allowed.", "paging_validation_error");

        var rawDirection = string.IsNullOrWhiteSpace(request.SortDirection) ? "asc" : request.SortDirection.ToLowerInvariant();
        if (rawDirection is not ("asc" or "desc"))
            throw new DomainException($"SortDirection must be 'asc' or 'desc'.", "paging_validation_error");

        var descending = rawDirection == "desc";

        var trimmedTerm = request.SearchTerm?.Trim();
        string? searchField = null;
        string? searchPattern = null;

        if (!string.IsNullOrEmpty(trimmedTerm))
        {
            if (string.IsNullOrWhiteSpace(request.SearchField))
                throw new DomainException("SearchField is required when SearchTerm is provided.", "paging_validation_error");

            var canonicalSearchField = allowedSearchFields.FirstOrDefault(f => string.Equals(f, request.SearchField, StringComparison.OrdinalIgnoreCase))
                ?? throw new DomainException($"Search field '{request.SearchField}' is not allowed.", "paging_validation_error");

            searchField = canonicalSearchField;
            searchPattern = BuildILikePattern(trimmedTerm);
        }

        return new PagingValidationResult(
            Page: request.Page,
            PageSize: request.PageSize,
            Skip: request.Page * request.PageSize,
            Take: request.PageSize,
            SortField: sortField,
            Descending: descending,
            SearchField: searchField,
            SearchPattern: searchPattern
        );
    }

    private static string BuildILikePattern(string term)
    {
        var escaped = term
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
        return $"%{escaped}%";
    }
}

public sealed record PagingValidationResult(
    int Page,
    int PageSize,
    int Skip,
    int Take,
    string SortField,
    bool Descending,
    string? SearchField,
    string? SearchPattern
);
