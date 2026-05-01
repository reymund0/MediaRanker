using FluentAssertions;
using MediaRankerServer.Shared.Exceptions;
using MediaRankerServer.Shared.Paging;

namespace MediaRankerServer.UnitTests.Shared.Paging;

public class PagingValidatorTests
{
    private static readonly IReadOnlyCollection<string> AllowedSortFields = ["title", "releaseDate", "createdAt"];
    private static readonly IReadOnlyCollection<string> AllowedSearchFields = ["title"];
    private const string DefaultSort = "title";

    private static PagingValidationResult Validate(PageRequest request)
        => PagingValidator.Validate(request, AllowedSortFields, AllowedSearchFields, DefaultSort);

    // ── Default handling ──────────────────────────────────────────────────────

    [Fact]
    public void DefaultSort_WhenSortFieldNull_UsesDefaultSortField()
    {
        var result = Validate(new PageRequest());
        result.SortField.Should().Be("title");
    }

    [Fact]
    public void DefaultSort_WhenSortFieldEmpty_UsesDefaultSortField()
    {
        var result = Validate(new PageRequest(SortField: ""));
        result.SortField.Should().Be("title");
    }

    [Fact]
    public void DefaultDirection_WhenSortDirectionNull_IsAsc()
    {
        var result = Validate(new PageRequest(SortDirection: null));
        result.Descending.Should().BeFalse();
    }

    // ── Sort field allow-list ─────────────────────────────────────────────────

    [Theory]
    [InlineData("title")]
    [InlineData("TITLE")]
    [InlineData("Title")]
    [InlineData("releaseDate")]
    [InlineData("RELEASEDATE")]
    public void SortField_KnownField_AcceptsAndReturnsCanonicalCasing(string field)
    {
        var result = Validate(new PageRequest(SortField: field));
        AllowedSortFields.Should().Contain(result.SortField);
    }

    [Fact]
    public void SortField_Unknown_ThrowsPagingValidationError()
    {
        var act = () => Validate(new PageRequest(SortField: "notAllowed"));
        act.Should().Throw<DomainException>().Which.Type.Should().Be("paging_validation_error");
    }

    // ── SortDirection ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("asc", false)]
    [InlineData("Asc", false)]
    [InlineData("ASC", false)]
    [InlineData("desc", true)]
    [InlineData("DESC", true)]
    public void SortDirection_ValidValues_ParsedCorrectly(string direction, bool expectedDescending)
    {
        var result = Validate(new PageRequest(SortDirection: direction));
        result.Descending.Should().Be(expectedDescending);
    }

    [Fact]
    public void SortDirection_Invalid_ThrowsPagingValidationError()
    {
        var act = () => Validate(new PageRequest(SortDirection: "sideways"));
        act.Should().Throw<DomainException>().Which.Type.Should().Be("paging_validation_error");
    }

    // ── Page / PageSize boundaries ────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void PageSize_AtBoundaries_IsAccepted(int pageSize)
    {
        var result = Validate(new PageRequest(PageSize: pageSize));
        result.PageSize.Should().Be(pageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void PageSize_OutOfRange_ThrowsPagingValidationError(int pageSize)
    {
        var act = () => Validate(new PageRequest(PageSize: pageSize));
        act.Should().Throw<DomainException>().Which.Type.Should().Be("paging_validation_error");
    }

    [Fact]
    public void Page_Negative_ThrowsPagingValidationError()
    {
        var act = () => Validate(new PageRequest(Page: -1));
        act.Should().Throw<DomainException>().Which.Type.Should().Be("paging_validation_error");
    }

    // ── Skip/Take math ────────────────────────────────────────────────────────

    [Fact]
    public void SkipTake_Page2Size10_CorrectValues()
    {
        var result = Validate(new PageRequest(Page: 2, PageSize: 10));
        result.Skip.Should().Be(20);
        result.Take.Should().Be(10);
    }

    // ── Search ────────────────────────────────────────────────────────────────

    [Fact]
    public void Search_NullTerm_ReturnsNullSearchFieldAndPattern()
    {
        var result = Validate(new PageRequest(SearchTerm: null, SearchField: "title"));
        result.SearchField.Should().BeNull();
        result.SearchPattern.Should().BeNull();
    }

    [Fact]
    public void Search_WhitespaceTerm_ReturnsNullSearchFieldAndPattern()
    {
        var result = Validate(new PageRequest(SearchTerm: "   ", SearchField: "title"));
        result.SearchField.Should().BeNull();
        result.SearchPattern.Should().BeNull();
    }

    [Fact]
    public void Search_TermWithoutField_ThrowsPagingValidationError()
    {
        var act = () => Validate(new PageRequest(SearchTerm: "matrix", SearchField: null));
        act.Should().Throw<DomainException>().Which.Type.Should().Be("paging_validation_error");
    }

    [Fact]
    public void Search_FieldNotInAllowList_ThrowsPagingValidationError()
    {
        var act = () => Validate(new PageRequest(SearchTerm: "matrix", SearchField: "description"));
        act.Should().Throw<DomainException>().Which.Type.Should().Be("paging_validation_error");
    }

    [Fact]
    public void Search_ValidTermAndField_ReturnsWrappedPattern()
    {
        var result = Validate(new PageRequest(SearchTerm: "matrix", SearchField: "title"));
        result.SearchField.Should().Be("title");
        result.SearchPattern.Should().Be("%matrix%");
    }

    [Fact]
    public void Search_WildcardEscaping_PercentIsEscaped()
    {
        var result = Validate(new PageRequest(SearchTerm: "50%", SearchField: "title"));
        result.SearchPattern.Should().Be(@"%50\%%");
    }

    [Fact]
    public void Search_WildcardEscaping_UnderscoreIsEscaped()
    {
        var result = Validate(new PageRequest(SearchTerm: "the_dark", SearchField: "title"));
        result.SearchPattern.Should().Be(@"%the\_dark%");
    }

    [Fact]
    public void Search_WildcardEscaping_BackslashIsEscapedFirst()
    {
        var result = Validate(new PageRequest(SearchTerm: @"a\b", SearchField: "title"));
        result.SearchPattern.Should().Be(@"%a\\b%");
    }
}
