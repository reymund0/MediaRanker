namespace MediaRankerServer.Modules.Media.Providers;

/// <summary>
/// A provider-owned artwork reference. ImagePath is an asset identifier, never a URL.
/// </summary>
public sealed record ArtworkResult(string? ProviderItemId, string? ImagePath);
