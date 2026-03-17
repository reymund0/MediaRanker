using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MediaRankerServer.Modules.Reviews.Entities;

namespace MediaRankerServer.Modules.Templates.Entities;

public class TemplateField
{
    public long Id { get; set; }
    public long TemplateId { get; set; }

    public string Name { get; set; } = null!;
    public int Position { get; set; }

    public Template Template { get; set; } = null!;
    public ICollection<ReviewField> ReviewFields { get; set; } = [];

    public class Configuration : IEntityTypeConfiguration<TemplateField>
    {
        public void Configure(EntityTypeBuilder<TemplateField> builder)
        {
            builder.ToTable("template_fields");

            builder.HasKey(tf => tf.Id);

            builder.Property(tf => tf.Id)
                .HasColumnName("id");

            builder.Property(tf => tf.TemplateId)
                .HasColumnName("template_id")
                .IsRequired();

            builder.Property(tf => tf.Name)
                .HasColumnName("name")
                .IsRequired();

            builder.Property(tf => tf.Position)
                .HasColumnName("position")
                .IsRequired();

            // Relationships
            builder.HasOne(tf => tf.Template)
                .WithMany(t => t.Fields)
                .HasForeignKey(tf => tf.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(tf => tf.ReviewFields)
                .WithOne(rf => rf.TemplateField)
                .HasForeignKey(rf => rf.TemplateFieldId);

            // Indexes
            builder.HasIndex(tf => tf.TemplateId)
                .HasDatabaseName("ix_template_fields_template_id");
        }
    }
}
