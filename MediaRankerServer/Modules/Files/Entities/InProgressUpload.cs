using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Files.Entities;

public enum FileEntityType
{
  MediaCover
}

public enum InProgressUploadState{
  Uploading,
  Uploaded,
  Failed,
  Copied // InProgressUpload data has been copied to module's permanent storage.
}

public class InProgressUpload : ITimestampedEntity
{
    public long Id { get; set; }
    public string UserId { get; set; } = null!;
    public FileEntityType EntityType { get; set; }
    public string FileKey { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ExpectedContentType { get; set; } = null!;
    public long ExpectedFileSize { get; set; }
    public string? ActualContentType { get; set; }
    public long? ActualFileSize { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public InProgressUploadState State { get; set; }

    public class Configuration : IEntityTypeConfiguration<InProgressUpload>
    {
        public void Configure(EntityTypeBuilder<InProgressUpload> builder)
        {
            builder.ToTable("in_progress_uploads");

            builder.HasKey(i => i.Id);

            builder.Property(i => i.UserId).IsRequired();
            builder.Property(i => i.EntityType)
                .HasConversion<string>()
                .IsRequired();
            builder.Property(i => i.FileKey).IsRequired();
            builder.Property(i => i.FileName).IsRequired();
            builder.Property(i => i.ExpectedContentType).IsRequired();
            builder.Property(i => i.ExpectedFileSize).IsRequired();
            builder.Property(i => i.ActualContentType);
            builder.Property(i => i.ActualFileSize);
            builder.Property(i => i.State)
                .HasConversion<string>() // Save as string in database so the enum values can be modified without breaking existing data.
                .HasDefaultValue(InProgressUploadState.Uploading)
                .IsRequired();
        }
    }
}
