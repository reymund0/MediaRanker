using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Media.Data.Entities;

public enum MediaCollectionType
{
    Series,
    Season
}

public class MediaCollection : ITimestampedEntity
{
    public long Id { get; set; }
    public string Title { get; set; } = null!;
    public MediaCollectionType CollectionType { get; set; }
    public long? ParentMediaCollectionId { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? ExternalId { get; set; }
    public MediaExternalSource? ExternalSource { get; set; }
    
    // Foreign keys
    public long? CoverId { get; set; }
    public long MediaTypeId { get; set; }


    // Navigation properties
    public MediaType MediaType { get; set; } = null!;

    public MediaCollection? ParentMediaCollection { get; set; }
    public ICollection<MediaCollection> ChildCollections { get; set; } = [];
    public ICollection<MediaEntity> MediaItems { get; set; } = [];
    public MediaCover? Cover { get; set; }

    public class Configuration : IEntityTypeConfiguration<MediaCollection>
    {
        public void Configure(EntityTypeBuilder<MediaCollection> builder)
        {
            builder.ToTable("media_collections");

            builder.HasKey(mc => mc.Id);

            builder.Property(mc => mc.Id);

            builder.Property(mc => mc.Title)
                .IsRequired();

            builder.Property(mc => mc.CollectionType)
                .HasConversion<string>()
                .IsRequired();

            builder.Property(mc => mc.MediaTypeId)
                .IsRequired();

            builder.Property(mc => mc.ParentMediaCollectionId);

            builder.Property(mc => mc.ReleaseDate)
                .HasColumnType("date");

            builder.Property(mc => mc.CoverId);
            
            builder.Property(mc => mc.ExternalId);
            
            builder.Property(mc => mc.ExternalSource)
                .HasConversion<string>();

            // Relationships
            builder.HasOne(mc => mc.MediaType)
                .WithMany()
                .HasForeignKey(mc => mc.MediaTypeId);

            builder.HasOne(mc => mc.ParentMediaCollection)
                .WithMany(mc => mc.ChildCollections)
                .HasForeignKey(mc => mc.ParentMediaCollectionId)
                .OnDelete(DeleteBehavior.SetNull);
            
            builder.HasOne(mc => mc.Cover)
                .WithMany()
                .HasForeignKey(mc => mc.CoverId);

            // Indexes
            builder.HasIndex(mc => mc.MediaTypeId)
                .HasDatabaseName("ix_media_collections_media_type_id");

            builder.HasIndex(mc => mc.ParentMediaCollectionId)
                .HasDatabaseName("ix_media_collections_parent_id");

            builder.HasIndex(mc => mc.ExternalId)
                .HasDatabaseName("ix_media_collections_external_id");

            // Partial unique indexes to prevent duplicate collections. One with parent included, one without.
            builder.HasIndex(mc => new { mc.Title, mc.CollectionType, mc.MediaTypeId, mc.ParentMediaCollectionId })
                .IsUnique()
                .HasDatabaseName("uq_media_collections_title_type_mediatype_parent")
                .HasFilter("parent_media_collection_id IS NOT NULL");

            builder.HasIndex(mc => new { mc.ExternalId, mc.ExternalSource })
                .IsUnique()
                .HasDatabaseName("uq_media_collections_external_id_source_series")
                .HasFilter("external_id IS NOT NULL AND collection_type = 'Series'");

            builder.HasIndex(mc => new { mc.Title, mc.CollectionType, mc.MediaTypeId })
                .IsUnique()
                .HasDatabaseName("uq_media_collections_title_type_mediatype_root")
                .HasFilter("parent_media_collection_id IS NULL");
        }
    }
}
