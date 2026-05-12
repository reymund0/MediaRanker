using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Media.Data.Entities;

/// <summary>
/// Represents an IMDB ratings row from title.ratings.tsv.gz.
/// IMDB documentation: https://developer.imdb.com/non-commercial-datasets/
/// </summary>
public class ImdbImportRating : ITimestampedEntity
{
    public long Id { get; set; }
    public string Tconst { get; set; } = null!;
    public decimal AverageRating { get; set; }
    public int NumVotes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public class Configuration : IEntityTypeConfiguration<ImdbImportRating>
    {
        public void Configure(EntityTypeBuilder<ImdbImportRating> builder)
        {
            builder.ToTable("imdb_import_ratings");

            builder.HasKey(r => r.Id);

            builder.Property(r => r.Tconst).IsRequired();
            builder.Property(r => r.AverageRating)
                .HasColumnType("numeric(3,1)")
                .IsRequired();
            builder.Property(r => r.NumVotes).IsRequired();

            builder.HasIndex(r => r.Tconst)
                .IsUnique()
                .HasDatabaseName("uq_imdb_import_ratings_tconst");
        }
    }
}
