using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaRankerServer.Modules.Media.Data.Entities;
using MediaRankerServer.Modules.Reviews.Data.Entities;
using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Modules.Templates.Data.Entities;

public class Template : ITimestampedEntity
{
    public long Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<TemplateField> Fields { get; set; } = [];

    // Related entities.
    public long MediaTypeId { get; set; }


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
            builder.HasMany(t => t.Fields)
                .WithOne(f => f.Template)
                .HasForeignKey(f => f.TemplateId);

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
            
            // Index for related entities
            builder.HasIndex(t => t.MediaTypeId)
                .HasDatabaseName("ix_templates_media_type_id");

            // Seed system template for Video Games
            builder.HasData(
                new Template
                {
                    Id = -1,
                    MediaTypeId = -1,
                    UserId = "system",
                    Name = "Video Games",
                    Description = "Default review template for video games."
                }
            );
        }
    }
}
