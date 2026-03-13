using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MediaRankerServer.Modules.Rankings.Entities;

namespace MediaRankerServer.Modules.Media.Entities;

public class MediaEntity
{
    public long Id { get; set; }

    public string Title { get; set; } = null!;
    public long MediaTypeId { get; set; }
    public MediaType MediaType { get; set; } = null!;
    public DateOnly ReleaseDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<RankedMedia> RankedMedia { get; set; } = [];

    public class Configuration : IEntityTypeConfiguration<MediaEntity>
    {
        public void Configure(EntityTypeBuilder<MediaEntity> builder)
        {
            builder.ToTable("media");

            builder.HasKey(m => m.Id);

            builder.Property(m => m.Id)
                .HasColumnName("id");

            builder.Property(m => m.Title)
                .HasColumnName("title")
                .IsRequired();

            builder.Property(m => m.MediaTypeId)
                .HasColumnName("media_type_id")
                .IsRequired();

            builder.Property(m => m.ReleaseDate)
                .HasColumnName("release_date")
                .HasColumnType("date")
                .IsRequired();

            builder.Property(m => m.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            builder.Property(m => m.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            // Relationships
            builder.HasOne(m => m.MediaType)
                .WithMany()
                .HasForeignKey(m => m.MediaTypeId);

            builder.HasMany(m => m.RankedMedia)
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
