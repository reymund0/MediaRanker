using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaRankerServer.Data.Entities;

public class Media
{
    public long Id { get; set; }

    public string Title { get; set; } = null!;
    public MediaType MediaType { get; set; }
    public DateOnly ReleaseDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<RankedMedia> RankedMedia { get; set; } = [];

    public class Configuration : IEntityTypeConfiguration<Media>
    {
        public void Configure(EntityTypeBuilder<Media> builder)
        {
            builder.ToTable("media");

            builder.HasKey(m => m.Id);

            builder.Property(m => m.Id)
                .HasColumnName("id");

            builder.Property(m => m.Title)
                .HasColumnName("title")
                .IsRequired();

            builder.Property(m => m.MediaType)
                .HasColumnName("media_type")
                .HasColumnType("media_type")
                .IsRequired();

            builder.Property(m => m.ReleaseDate)
                .HasColumnName("release_date")
                .HasColumnType("date")
                .IsRequired();

            builder.Property(m => m.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            builder.Property(m => m.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("NOW()")
                .IsRequired();

            builder.HasIndex(m => new { m.MediaType })
                .HasDatabaseName("ix_media_media_type");

            builder.HasIndex(m => new { m.ReleaseDate })
                .HasDatabaseName("ix_media_release_date");

            // Helps prevent duplicate media entries.
            builder.HasIndex(m => new { m.Title, m.MediaType, m.ReleaseDate })
                .IsUnique()
                .HasDatabaseName("uq_media_title_type_release_date");
        }
    }
}