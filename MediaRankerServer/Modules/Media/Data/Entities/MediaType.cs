using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaRankerServer.Modules.Media.Data.Entities;

public class MediaType
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public class Configuration : IEntityTypeConfiguration<MediaType>
    {
        public void Configure(EntityTypeBuilder<MediaType> builder)
        {
            builder.ToTable("media_types");

            builder.HasKey(mt => mt.Id);

            builder.Property(mt => mt.Id);

            builder.Property(mt => mt.Name)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(mt => mt.Name)
                .IsUnique()
                .HasDatabaseName("uq_media_types_name");

            // Seed system media types
            builder.HasData(
                new MediaType { Id = -1, Name = "Video Game" },
                new MediaType { Id = -2, Name = "Book" },
                new MediaType { Id = -3, Name = "Movie" },
                new MediaType { Id = -4, Name = "TV Show" },
                new MediaType { Id = -5, Name = "Album" },
                new MediaType { Id = -6, Name = "Concert" }
            );
        }
    }
}
