using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaRankerServer.Modules.Reviews.Data.Entities;

public class ReviewField
{
    public long ReviewId { get; set; }
    public short Value { get; set; } // 1–10
    public Review Review { get; set; } = null!;

    // Related entities.
    public long TemplateFieldId { get; set; }


    public class Configuration : IEntityTypeConfiguration<ReviewField>
    {
        public void Configure(EntityTypeBuilder<ReviewField> builder)
        {
            builder.ToTable("review_fields", t =>
            {
                // Check constraint for 1–10 score range
                t.HasCheckConstraint("ck_review_fields_value", "value BETWEEN 1 AND 10");
            });

            builder.HasKey(rms => new { rms.ReviewId, rms.TemplateFieldId });

            builder.Property(rms => rms.ReviewId);

            builder.Property(rms => rms.TemplateFieldId);

            builder.Property(rms => rms.Value)
                .IsRequired();

            // Relationships
            builder.HasOne(rms => rms.Review)
                .WithMany(rm => rm.Fields)
                .HasForeignKey(rms => rms.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for related entities.
            builder.HasIndex(rms => rms.TemplateFieldId)
                .HasDatabaseName("ix_review_fields_field");  
        }
    }
}
