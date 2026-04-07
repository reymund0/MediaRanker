using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Media.Data.Entities;

public class MediaEntity : ITimestampedEntity
{
    public long Id { get; set; }

    public string Title { get; set; } = null!;
    public long MediaTypeId { get; set; }
    public MediaType MediaType { get; set; } = null!;
    public DateOnly ReleaseDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? ExternalId { get; set; }
    public string? ExternalSource { get; set; }

    // Cover File Upload - We intentionally want to copy data over rather than joining on FileUploads. Hence no reference below.
    public long? CoverFileUploadId { get; set; }
    public string? CoverFileKey { get; set; }
    public string? CoverFileName { get; set; }
    public long? CoverFileSizeBytes { get; set; }
    public string? CoverFileContentType { get; set; }

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
            
            builder.Property(m => m.CoverFileUploadId);
            builder.Property(m => m.CoverFileKey);
            builder.Property(m => m.CoverFileName);
            builder.Property(m => m.CoverFileSizeBytes);
            builder.Property(m => m.CoverFileContentType);
            builder.Property(m => m.ExternalId);
            builder.Property(m => m.ExternalSource);

            // Relationships
            builder.HasOne(m => m.MediaType)
                .WithMany()
                .HasForeignKey(m => m.MediaTypeId);

            // Indexes
            builder.HasIndex(m => m.MediaTypeId)
                .HasDatabaseName("ix_media_media_type_id");

            builder.HasIndex(m => m.ReleaseDate)
                .HasDatabaseName("ix_media_release_date");

            // Helps prevent duplicate media entries.
            builder.HasIndex(m => new { m.Title, m.MediaTypeId, m.ReleaseDate })
                .IsUnique()
                .HasDatabaseName("uq_media_title_type_release_date");

            // Unique partial index on ExternalId + ExternalSource (only when ExternalId is not null)
            builder.HasIndex(m => new { m.ExternalId, m.ExternalSource })
                .IsUnique()
                .HasDatabaseName("uq_media_external_id_source")
                .HasFilter("external_id IS NOT NULL");
        }
    }
}
