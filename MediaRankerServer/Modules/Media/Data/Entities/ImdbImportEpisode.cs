using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Media.Data.Entities;

/// <summary>
/// Represents an IMDB episode import record from title.episode.tsv.gz.
/// IMDB documentation: https://developer.imdb.com/non-commercial-datasets/
/// Note: SeasonNumber/EpisodeNumber use 0 for both unknown (\N) values and "Specials" episodes,
/// as per IMDB convention. This loses the distinction but keeps the data non-nullable.
/// </summary>
public class ImdbImportEpisode : ITimestampedEntity
{
    public long Id { get; set; }
    public string Tconst { get; set; } = null!;
    public string ParentTconst { get; set; } = null!;
    public int SeasonNumber { get; set; }
    public int EpisodeNumber { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public class Configuration : IEntityTypeConfiguration<ImdbImportEpisode>
    {
        public void Configure(EntityTypeBuilder<ImdbImportEpisode> builder)
        {
            builder.ToTable("imdb_import_episodes");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Tconst).IsRequired();
            builder.Property(e => e.ParentTconst).IsRequired();
            builder.Property(e => e.SeasonNumber).IsRequired();
            builder.Property(e => e.EpisodeNumber).IsRequired();

            // Unique index on Tconst
            builder.HasIndex(e => e.Tconst)
                .IsUnique()
                .HasDatabaseName("uq_imdb_import_episodes_tconst");

            // Non-unique index on ParentTconst to speed up joins on base imdbImport table for series data.
            builder.HasIndex(e => e.ParentTconst)
                .HasDatabaseName("ix_imdb_import_episodes_parent_tconst");
        }
    }
}
