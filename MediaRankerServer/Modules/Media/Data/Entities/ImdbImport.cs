using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Media.Data.Entities;

/// <summary>
/// Represents an IMDB import record for non commercial use. IMDB documentation: https://developer.imdb.com/non-commercial-datasets/
/// Currently only using the tile basics file.
/// </summary>
public class ImdbImport : ITimestampedEntity
{
    public long Id { get; set; }
    public string Tconst { get; set; } = null!;
    public string TitleType { get; set; } = null!;
    public string PrimaryTitle { get; set; } = null!;
    public string OriginalTitle { get; set; } = null!;
    public bool IsAdult { get; set; }
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
    public int? RuntimeMinutes { get; set; }
    public string? Genres { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string RawLine { get; set; } = null!;

    public class Configuration : IEntityTypeConfiguration<ImdbImport>
    {
        public void Configure(EntityTypeBuilder<ImdbImport> builder)
        {
            builder.ToTable("imdb_imports");

            builder.HasKey(i => i.Id);

            builder.Property(i => i.Tconst).IsRequired();
            builder.Property(i => i.TitleType).IsRequired();
            builder.Property(i => i.PrimaryTitle).IsRequired();
            builder.Property(i => i.OriginalTitle).IsRequired();
            builder.Property(i => i.IsAdult).IsRequired();
            builder.Property(i => i.StartYear);
            builder.Property(i => i.EndYear);
            builder.Property(i => i.RuntimeMinutes);
            builder.Property(i => i.Genres);
            builder.Property(i => i.RawLine).IsRequired();
            
            // Indexes
            builder.HasIndex(i => i.Tconst)
                .IsUnique()
                .HasDatabaseName("uq_imdb_imports_tconst");

            builder.HasIndex(i => i.TitleType)
                .HasDatabaseName("ix_imdb_imports_title_type");
        }
    }
}
