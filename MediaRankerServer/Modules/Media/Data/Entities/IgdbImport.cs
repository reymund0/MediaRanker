using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Media.Data.Entities;

/// <summary>Provider staging data. It is deliberately retained for future releases to become eligible.</summary>
public sealed class IgdbImport : ITimestampedEntity
{
    public long Id { get; set; }
    public long IgdbGameId { get; set; }
    public string? Name { get; set; }
    public DateTimeOffset? FirstReleaseDate { get; set; }
    public long? GameTypeId { get; set; }
    public string? GameTypeName { get; set; }
    public long? VersionParentId { get; set; }
    public string? CoverImageId { get; set; }
    public DateTimeOffset? ProviderUpdatedAt { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public sealed class Configuration : IEntityTypeConfiguration<IgdbImport>
    {
        public void Configure(EntityTypeBuilder<IgdbImport> builder)
        {
            builder.ToTable("igdb_imports");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.IgdbGameId).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(500);
            builder.Property(x => x.GameTypeName).HasMaxLength(100);
            builder.Property(x => x.CoverImageId).HasMaxLength(255);
            builder.Property(x => x.FetchedAt).IsRequired();
            builder.HasIndex(x => x.IgdbGameId).IsUnique().HasDatabaseName("uq_igdb_imports_game_id");
            builder.HasIndex(x => new { x.FirstReleaseDate, x.GameTypeName }).HasDatabaseName("ix_igdb_imports_admission");
            builder.HasIndex(x => x.ProviderUpdatedAt).HasDatabaseName("ix_igdb_imports_provider_updated_at");
        }
    }
}
