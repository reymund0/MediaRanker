namespace MediaRankerServer.Modules.Media.Contracts;

public record CoverPresentation(string? Url, string Status)
{
    public static readonly CoverPresentation Unsupported = new(null, "unsupported");
}
