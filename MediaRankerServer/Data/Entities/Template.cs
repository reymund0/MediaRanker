using System;
using System.Collections.Generic;
using MediaRankerServer.Data.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaRankerServer.Data.Entities;

public class Template
{
    public long Id { get; set; }
    public string UserId { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<TemplateField> Fields { get; set; } = [];
    public ICollection<RankedMedia> RankedMedia { get; set; } = [];

    public class Configuration : IEntityTypeConfiguration<Template>
    {
        public void Configure(EntityTypeBuilder<Template> builder)
        {
            builder.ToTable("templates");

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Id)
                .HasColumnName("id");

            builder.Property(t => t.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            builder.Property(t => t.Name)
                .HasColumnName("name")
                .IsRequired();

            builder.Property(t => t.Description)
                .HasColumnName("description");

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
                .HasFilter($"user_id = '{SeedIds.SystemUserId}'");
        }
    }
}