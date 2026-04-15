using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Media.Data.Entities;

// Media covers are deleted via a scheduled background job that validates over 2 passes that no records are attached anymore.
public class MediaCover : ITimestampedEntity
{
    public long Id { get; set; }
    public long FileUploadId { get; set; }
    public string FileKey { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public string FileContentType { get; set; } = null!;
    public bool MarkedForCleanup { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public List<MediaEntity> MediaEntities { get; set; } = [];
    public List<MediaCollection> MediaCollections { get; set; } = [];


    public class Configuration : IEntityTypeConfiguration<MediaCover>
    {
        public void Configure(EntityTypeBuilder<MediaCover> builder)
        {
            builder.ToTable("media_covers");

            builder.HasKey(mc => mc.Id);

            builder.Property(mc => mc.Id);
            builder.Property(mc => mc.FileUploadId).IsRequired();
            builder.Property(mc => mc.FileKey).IsRequired();
            builder.Property(mc => mc.FileName).IsRequired();
            builder.Property(mc => mc.FileSizeBytes).IsRequired();
            builder.Property(mc => mc.FileContentType).IsRequired();
            builder.Property(mc => mc.MarkedForCleanup).HasDefaultValue(false);
            builder.Property(mc => mc.CreatedAt);
            builder.Property(mc => mc.UpdatedAt);

            // Relationships
            builder.HasMany(mc => mc.MediaEntities)
                .WithOne(me => me.Cover)
                .HasForeignKey(me => me.CoverId);

            builder.HasMany(mc => mc.MediaCollections)
                .WithOne(mc => mc.Cover)
                .HasForeignKey(mc => mc.CoverId);
        }
    }
}