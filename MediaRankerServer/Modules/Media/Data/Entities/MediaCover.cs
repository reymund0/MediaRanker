using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Media.Data.Entities;

public enum ArtworkProvider { Tmdb, Igdb }
public enum CoverLookupKind { MovieImdb, SeriesImdb, IgdbGame }
public enum CoverOutcome { Pending, Ready, Missing, Failed }

public class MediaCover : ITimestampedEntity
{
    public long Id { get; set; }
    public ArtworkProvider Provider { get; set; }
    public CoverLookupKind LookupKind { get; set; }
    public string LookupId { get; set; } = null!;
    public string? ProviderItemId { get; set; }
    public string? ImagePath { get; set; }
    public CoverOutcome Outcome { get; set; }
    public DateTimeOffset? CheckedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RequestedAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public int AttemptCount { get; set; }
    public string? FailureCode { get; set; }
    public Guid? ClaimToken { get; set; }
    public DateTimeOffset? ClaimedUntil { get; set; }
    public long Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<MediaEntity> MediaEntities { get; set; } = [];
    public List<MediaCollection> MediaCollections { get; set; } = [];

    public class Configuration : IEntityTypeConfiguration<MediaCover>
    {
        public void Configure(EntityTypeBuilder<MediaCover> builder)
        {
            builder.ToTable("media_covers", table => table.HasCheckConstraint("ck_media_cover_ready", "outcome <> 'Ready' OR (image_path IS NOT NULL AND checked_at IS NOT NULL AND expires_at IS NOT NULL)"));
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Provider).HasConversion<string>();
            builder.Property(x => x.LookupKind).HasConversion<string>();
            builder.Property(x => x.Outcome).HasConversion<string>();
            builder.Property(x => x.LookupId).IsRequired();
            builder.Property(x => x.Version).IsConcurrencyToken();
            builder.HasIndex(x => new { x.Provider, x.LookupKind, x.LookupId }).IsUnique();
            builder.HasIndex(x => new { x.NextAttemptAt, x.ClaimedUntil });
            builder.HasMany(x => x.MediaEntities).WithOne(x => x.Cover).HasForeignKey(x => x.CoverId);
            builder.HasMany(x => x.MediaCollections).WithOne(x => x.Cover).HasForeignKey(x => x.CoverId);
        }
    }
}
