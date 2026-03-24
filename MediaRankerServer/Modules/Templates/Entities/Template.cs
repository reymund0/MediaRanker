using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaRankerServer.Modules.Media.Entities;
using MediaRankerServer.Modules.Reviews.Entities;
using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Templates.Entities;

public class Template : ITimestampedEntity
{
    public long Id { get; set; }
    public string UserId { get; set; } = null!;

    public long MediaTypeId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public MediaType MediaType { get; set; } = null!;
    public ICollection<TemplateField> Fields { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];

    public class Configuration : IEntityTypeConfiguration<Template>
    {
        public void Configure(EntityTypeBuilder<Template> builder)
        {
            builder.ToTable("templates");

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Id);

            builder.Property(t => t.UserId)
                .IsRequired();

            builder.Property(t => t.MediaTypeId);

            builder.Property(t => t.Name)
                .IsRequired();

            builder.Property(t => t.Description);

            // Relationships
            builder.HasOne(t => t.MediaType)
                .WithMany()
                .HasForeignKey(t => t.MediaTypeId);

            builder.HasMany(t => t.Fields)
                .WithOne(f => f.Template)
                .HasForeignKey(f => f.TemplateId);

            builder.HasMany(t => t.Reviews)
                .WithOne(r => r.Template)
                .HasForeignKey(r => r.TemplateId);

            // Indexes
            builder.HasIndex(t => t.UserId)
                .HasDatabaseName("ix_templates_user_id");

            builder.HasIndex(t => new { t.UserId, t.Name })
                .IsUnique()
                .HasDatabaseName("uq_templates_user_name");

            // Partial unique index for system templates (name unique among system templates)
            builder.HasIndex(t => t.Id)
                .HasDatabaseName("uq_templates_is_system")
                .HasFilter("id < 0");
        }
    }
}
