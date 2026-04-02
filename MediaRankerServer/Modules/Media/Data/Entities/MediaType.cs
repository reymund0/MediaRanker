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
        }
    }
}
