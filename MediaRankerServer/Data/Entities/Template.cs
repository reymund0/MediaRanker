using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaRankerServer.Data.Entities;

public class Template
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<TemplateField> Fields { get; set; } = new List<TemplateField>();
    public ICollection<RankedMedia> RankedMedia { get; set; } = new List<RankedMedia>();

    public class Configuration : IEntityTypeConfiguration<Template>
    {
        public void Configure(EntityTypeBuilder<Template> builder)
        {
            builder.ToTable("templates", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_templates_system_user_id",
                    "(is_system = TRUE AND user_id IS NULL) OR (is_system = FALSE AND user_id IS NOT NULL)"
                );
            });

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Id)
                .HasColumnName("id");

            builder.Property(t => t.UserId)
                .HasColumnName("user_id");

            builder.Property(t => t.Name)
                .HasColumnName("name")
                .IsRequired();

            builder.Property(t => t.Description)
                .HasColumnName("description");

            builder.Property(t => t.IsSystem)
                .HasColumnName("is_system")
                .HasDefaultValue(false)
                .IsRequired();

            builder.Property(t => t.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            builder.Property(t => t.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            // Relationships
            builder.HasMany(t => t.Fields)
                .WithOne(f => f.Template)
                .HasForeignKey(f => f.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(t => t.RankedMedia)
                .WithOne(rm => rm.Template)
                .HasForeignKey(rm => rm.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(t => t.UserId)
                .HasDatabaseName("ix_templates_user_id");

            builder.HasIndex(t => new { t.UserId, t.Name })
                .IsUnique()
                .HasDatabaseName("uq_templates_user_name");

            // Partial unique index for system templates (name unique among system templates)
            builder.HasIndex(t => t.Name)
                .HasDatabaseName("uq_templates_system_name")
                .HasFilter("is_system = TRUE");
        }
    }
}