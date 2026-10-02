using MediaRankerServer.Modules.Templates.Data.Entities;

namespace MediaRankerServer.Modules.Templates.Data.Seeds;

// Migration-owned seed values: preserve these once the migration is committed.
public static class EssentialsTemplateSeeds
{
    public static Template[] Templates =>
    [
        Create(-2, "Movie", "Movie essentials"),
        Create(-3, "TvShow", "TV essentials"),
        Create(-4, "Book", "Book essentials"),
        Create(-5, "Album", "Album essentials"),
        Create(-6, "Concert", "Concert essentials")
    ];

    public static TemplateField[] Fields =>
    [
        .. CreateFields(-2, "Story", "Performances", "Visuals", "Sound"),
        .. CreateFields(-3, "Story", "Characters", "Pacing", "Production"),
        .. CreateFields(-4, "Writing", "Ideas & themes", "Structure", "Engagement"),
        .. CreateFields(-5, "Composition", "Performance", "Production", "Cohesion"),
        .. CreateFields(-6, "Performance", "Setlist", "Live sound", "Atmosphere")
    ];

    private static Template Create(long id, string mediaType, string name) => new()
    {
        Id = id,
        UserId = "system",
        MediaType = mediaType,
        Name = name,
        Description = "Four equally weighted scores, rated from 1 to 10."
    };

    private static IEnumerable<TemplateField> CreateFields(long templateId, params string[] names) =>
        names.Select((name, position) => new TemplateField
        {
            Id = templateId * 10 - position - 1,
            TemplateId = templateId,
            Name = name,
            Position = position
        });
}
