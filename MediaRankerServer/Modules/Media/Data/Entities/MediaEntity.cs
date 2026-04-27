using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Media.Data.Entities;

public enum MediaExternalSource
{
    Imdb
}

public class MediaEntity : ITimestampedEntity
{

    public long Id { get; set; }

    public string Title { get; set; } = null!;
    public DateOnly? ReleaseDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? ExternalId { get; set; }
    public MediaExternalSource? ExternalSource { get; set; }
    
    // Foreign keys
    public long MediaTypeId { get; set; }
    public long? MediaCollectionId { get; set; }
    public long? CoverId { get; set; }

    // Navigation properties
    public MediaType MediaType { get; set; } = null!;
    public MediaCollection? MediaCollection { get; set; }
    public MediaCover? Cover { get; set; }


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
                .HasColumnType("date");
            
            builder.Property(m => m.CoverId);
            builder.Property(m => m.ExternalId);
            builder.Property(m => m.ExternalSource)
                .HasConversion<string>();
            builder.Property(m => m.MediaCollectionId);

            // Relationships
            builder.HasOne(m => m.MediaType)
                .WithMany()
                .HasForeignKey(m => m.MediaTypeId);

            builder.HasOne(m => m.MediaCollection)
                .WithMany(mc => mc.MediaItems)
                .HasForeignKey(m => m.MediaCollectionId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(m => m.Cover)
                .WithMany(m => m.MediaEntities)
                .HasForeignKey(m => m.CoverId);

            // Indexes
            builder.HasIndex(m => m.MediaTypeId)
                .HasDatabaseName("ix_media_media_type_id");

            builder.HasIndex(m => m.ReleaseDate)
                .HasDatabaseName("ix_media_release_date");

            builder.HasIndex(m => m.MediaCollectionId)
                .HasDatabaseName("ix_media_media_collection_id");

            // Unique partial index on ExternalId + ExternalSource (only when ExternalId is not null)
            builder.HasIndex(m => new { m.ExternalId, m.ExternalSource })
                .IsUnique()
                .HasDatabaseName("uq_media_external_id_source")
                .HasFilter("external_id IS NOT NULL");
        }
    }
}
