using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaRankerServer.Data.Entities;

public class RankedMedia
{
    public long Id { get; set; }
    public string UserId { get; set; } = null!;

    public long MediaId { get; set; }
    public long TemplateId { get; set; }

    public short OverallScore { get; set; } // 1–10

    public string? ReviewTitle { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Media Media { get; set; } = null!;
    public Template Template { get; set; } = null!;
    public ICollection<RankedMediaScore> Scores { get; set; } = [];

    public class Configuration : IEntityTypeConfiguration<RankedMedia>
    {
        public void Configure(EntityTypeBuilder<RankedMedia> builder)
        {
            builder.ToTable("ranked_media", t =>
            {
                t.HasCheckConstraint(
                    "ck_ranked_media_overall_score",
                    "overall_score BETWEEN 1 AND 10"
                );
            });

            builder.HasKey(rm => rm.Id);

            builder.Property(rm => rm.Id)
                .HasColumnName("id");

            builder.Property(rm => rm.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            builder.Property(rm => rm.MediaId)
                .HasColumnName("media_id")
                .IsRequired();

            builder.Property(rm => rm.TemplateId)
                .HasColumnName("template_id")
                .IsRequired();

            builder.Property(rm => rm.OverallScore)
                .HasColumnName("overall_score")
                .IsRequired();

            builder.Property(rm => rm.ReviewTitle)
                .HasColumnName("review_title");

            builder.Property(rm => rm.Notes)
                .HasColumnName("notes");

            builder.Property(rm => rm.ConsumedAt)
                .HasColumnName("consumed_at");

            builder.Property(rm => rm.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            builder.Property(rm => rm.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            // Relationships
            builder.HasOne(rm => rm.Media)
                .WithMany(m => m.RankedMedia)
                .HasForeignKey(rm => rm.MediaId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(rm => rm.Template)
                .WithMany(t => t.RankedMedia)
                .HasForeignKey(rm => rm.TemplateId);

            builder.HasMany(rm => rm.Scores)
                .WithOne(s => s.RankedMedia)
                .HasForeignKey(s => s.RankedMediaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(rm => rm.UserId)
                .HasDatabaseName("ix_ranked_media_user");

            builder.HasIndex(rm => new { rm.UserId, rm.TemplateId })
                .HasDatabaseName("ix_ranked_media_user_template");

            builder.HasIndex(rm => rm.MediaId)
                .HasDatabaseName("ix_ranked_media_media_id");

            builder.HasIndex(rm => new { rm.UserId, rm.MediaId, rm.TemplateId })
                .IsUnique()
                .HasDatabaseName("uq_ranked_media_user_media_template");
        }
    }
}