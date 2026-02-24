using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaRankerServer.Data.Entities;

public class RankedMediaScore
{
    public long RankedMediaId { get; set; }
    public long TemplateFieldId { get; set; }

    public short Value { get; set; } // 1–10

    public RankedMedia RankedMedia { get; set; } = null!;
    public TemplateField TemplateField { get; set; } = null!;

    public class Configuration : IEntityTypeConfiguration<RankedMediaScore>
    {
        public void Configure(EntityTypeBuilder<RankedMediaScore> builder)
        {
            builder.ToTable("ranked_media_scores", t =>
            {
                // Check constraint for 1–10 score range
                t.HasCheckConstraint("ck_ranked_media_scores_value", "value BETWEEN 1 AND 10");
            });

            builder.HasKey(rms => new { rms.RankedMediaId, rms.TemplateFieldId });

            builder.Property(rms => rms.RankedMediaId)
                .HasColumnName("ranked_media_id");

            builder.Property(rms => rms.TemplateFieldId)
                .HasColumnName("template_field_id");

            builder.Property(rms => rms.Value)
                .HasColumnName("value")
                .IsRequired();

            // Relationships
            builder.HasOne(rms => rms.RankedMedia)
                .WithMany(rm => rm.Scores)
                .HasForeignKey(rms => rms.RankedMediaId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(rms => rms.TemplateField)
                .WithMany(tf => tf.RankedMediaScores)
                .HasForeignKey(rms => rms.TemplateFieldId);

            // Indexes
            builder.HasIndex(rms => rms.TemplateFieldId)
                .HasDatabaseName("ix_ranked_media_scores_field");
        }
    }
}