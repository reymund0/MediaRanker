using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Templates.Data.Entities;

public class TemplateField : ITimestampedEntity
{
    public long Id { get; set; }
    public long TemplateId { get; set; }
    public string Name { get; set; } = null!;
    public int Position { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Template Template { get; set; } = null!;

    public class Configuration : IEntityTypeConfiguration<TemplateField>
    {
        public void Configure(EntityTypeBuilder<TemplateField> builder)
        {
            builder.ToTable("template_fields");

            builder.HasKey(tf => tf.Id);

            builder.Property(tf => tf.Id);

            builder.Property(tf => tf.TemplateId)
                .IsRequired();

            builder.Property(tf => tf.Name)
                .IsRequired();

            builder.Property(tf => tf.Position)
                .IsRequired();

            // Relationships
            builder.HasOne(tf => tf.Template)
                .WithMany(t => t.Fields)
                .HasForeignKey(tf => tf.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(tf => tf.TemplateId)
                .HasDatabaseName("ix_template_fields_template_id");
        }
    }
}
