using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Files.Data.Entities;

public enum FileEntityType
{
  MediaCover
}

public enum FileUploadState{
  Uploading,
  Uploaded, // File has been uploaded but will be deleted by cleanup job if not copied to module's permanent storage.
  Failed,
  Copied, // FileUpload data has been copied to module's permanent storage.
  Deleted // Module has deleted the FileUpload data.
}

public class FileUpload : ITimestampedEntity
{
    public long Id { get; set; }
    public string UserId { get; set; } = null!;
    public long? EntityId { get; set; }
    public FileEntityType EntityType { get; set; }
    public string FileKey { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ExpectedContentType { get; set; } = null!;
    public long ExpectedFileSizeBytes { get; set; }
    public string? ActualContentType { get; set; }
    public long? ActualFileSizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public FileUploadState State { get; set; }

    public class Configuration : IEntityTypeConfiguration<FileUpload>
    {
        public void Configure(EntityTypeBuilder<FileUpload> builder)
        {
            builder.ToTable("file_uploads");

            builder.HasKey(f => f.Id);

            builder.Property(f => f.UserId).IsRequired();
            builder.Property(f => f.EntityId);
            builder.Property(f => f.EntityType)
                .HasConversion<string>()
                .IsRequired();
            builder.Property(f => f.FileKey).IsRequired();
            builder.Property(f => f.FileName).IsRequired();
            builder.Property(f => f.ExpectedContentType).IsRequired();
            builder.Property(f => f.ExpectedFileSizeBytes).IsRequired();
            builder.Property(f => f.ActualContentType);
            builder.Property(f => f.ActualFileSizeBytes);
            builder.Property(f => f.State)
                .HasConversion<string>() // Save as string in database so the enum values can be modified without breaking existing data.
                .HasDefaultValue(FileUploadState.Uploading)
                .IsRequired();


            // Prevent storage collisions for the same entity type and file key.
            builder.HasIndex(f => new { f.EntityType, f.FileKey })
                .IsUnique()
                .HasDatabaseName("uq_file_uploads_entity_type_file_key");
        }
    }
}
