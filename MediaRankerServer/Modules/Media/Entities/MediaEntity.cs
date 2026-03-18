using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MediaRankerServer.Modules.Reviews.Entities;
using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Media.Entities;

public class MediaEntity : ITimestampedEntity
{
    public long Id { get; set; }

    public string Title { get; set; } = null!;
    public long MediaTypeId { get; set; }
    public MediaType MediaType { get; set; } = null!;
    public DateOnly ReleaseDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Review> Reviews { get; set; } = [];

    public class Configuration : IEntityTypeConfiguration<MediaEntity>
    {
        public void Configure(EntityTypeBuilder<MediaEntity> builder)
        {
            builder.ToTable("media");

            builder.HasKey(m => m.Id);

            builder.Property(m => m.Id);

            builder.Property(m => m.Title)
                .IsRequired();

            builder.Property(m => m.MediaTypeId)
                .IsRequired();

            builder.Property(m => m.ReleaseDate)
                .HasColumnType("date")
                .IsRequired();

            // Relationships
            builder.HasOne(m => m.MediaType)
                .WithMany()
                .HasForeignKey(m => m.MediaTypeId);

            builder.HasMany(m => m.Reviews)
                .WithOne(rm => rm.Media)
                .HasForeignKey(rm => rm.MediaId);

            // Indexes
            builder.HasIndex(m => m.MediaTypeId)
                .HasDatabaseName("ix_media_media_type_id");

            builder.HasIndex(m => m.ReleaseDate)
                .HasDatabaseName("ix_media_release_date");

            // Helps prevent duplicate media entries.
            builder.HasIndex(m => new { m.Title, m.MediaTypeId, m.ReleaseDate })
                .IsUnique()
                .HasDatabaseName("uq_media_title_type_release_date");
        }
    }
}
