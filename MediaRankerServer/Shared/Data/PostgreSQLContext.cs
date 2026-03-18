using Microsoft.EntityFrameworkCore;
using MediaRankerServer.Modules.Media.Entities;
using MediaRankerServer.Modules.Templates.Entities;
using MediaRankerServer.Modules.Reviews.Entities;
using MediaRankerServer.Shared.Data.Interfaces;

namespace MediaRankerServer.Shared.Data;

public class PostgreSQLContext : DbContext
{
    public PostgreSQLContext(DbContextOptions<PostgreSQLContext> options) : base(options) { }


    public DbSet<MediaType> MediaTypes => Set<MediaType>();
    public DbSet<MediaEntity> Media => Set<MediaEntity>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<TemplateField> TemplateFields => Set<TemplateField>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewField> ReviewFields => Set<ReviewField>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var modifiedEntries = ChangeTracker.Entries<ITimestampedEntity>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in modifiedEntries)
        {
            entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply global configuration for ITimestampedEntity
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITimestampedEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(ITimestampedEntity.CreatedAt))
                    .HasDefaultValueSql("NOW()");

                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(ITimestampedEntity.UpdatedAt))
                    .HasDefaultValueSql("NOW()");
            }
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PostgreSQLContext).Assembly);
    }
}
