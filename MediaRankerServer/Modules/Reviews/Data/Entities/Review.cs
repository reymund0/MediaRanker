using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Templates.Data.Entities;
using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Reviews.Data.Entities;

public class Review : ITimestampedEntity
{
    public long Id { get; set; }
    public string UserId { get; set; } = null!;
    public short OverallScore { get; set; } // 1–10
    public string? ReviewTitle { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<ReviewField> Fields { get; set; } = [];
    // Related entities
    public long? MediaId { get; set; }
    public long? MediaCollectionId { get; set; }
    public long TemplateId { get; set; }

    public class Configuration : IEntityTypeConfiguration<Review>
    {
        public void Configure(EntityTypeBuilder<Review> builder)
        {
            builder.ToTable("reviews", t =>
            {
                t.HasCheckConstraint(
                    "ck_reviews_overall_score",
                    "overall_score BETWEEN 1 AND 10"
                );
                t.HasCheckConstraint(
                    "ck_reviews_target",
                    "(media_id IS NOT NULL) <> (media_collection_id IS NOT NULL)"
                );
            });

            builder.HasKey(rm => rm.Id);

            builder.Property(rm => rm.Id);

            builder.Property(rm => rm.UserId)
                .IsRequired();

            builder.Property(rm => rm.MediaId);

            builder.Property(rm => rm.MediaCollectionId);

            builder.Property(rm => rm.TemplateId)
                .IsRequired();

            builder.Property(rm => rm.OverallScore)
                .IsRequired();

            builder.Property(rm => rm.ReviewTitle);

            builder.Property(rm => rm.Notes);

            builder.Property(rm => rm.ConsumedAt);

            // Relationships
            builder.HasMany(rm => rm.Fields)
                .WithOne(f => f.Review)
                .HasForeignKey(f => f.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(rm => rm.UserId)
                .HasDatabaseName("ix_reviews_user");

            builder.HasIndex(rm => new { rm.UserId, rm.TemplateId })
                .HasDatabaseName("ix_reviews_user_template");

            builder.HasIndex(rm => new { rm.UserId, rm.MediaId })
                .IsUnique()
                .HasDatabaseName("uq_reviews_user_media");

            builder.HasIndex(rm => new { rm.UserId, rm.MediaCollectionId })
                .IsUnique()
                .HasDatabaseName("uq_reviews_user_media_collection")
                .HasFilter("media_collection_id IS NOT NULL");

            // Indexes for related entities
            builder.HasIndex(rm => rm.MediaId)
                .HasDatabaseName("ix_reviews_media_id");

            builder.HasIndex(rm => rm.MediaCollectionId)
                .HasDatabaseName("ix_reviews_media_collection_id");

            builder.HasIndex(rm => rm.TemplateId)
                .HasDatabaseName("ix_reviews_template_id");
        }
    }
}
