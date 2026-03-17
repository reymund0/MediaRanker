using Microsoft.EntityFrameworkCore;
using MediaRankerServer.Modules.Media.Entities;
using MediaRankerServer.Modules.Templates.Entities;
using MediaRankerServer.Modules.Reviews.Entities;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PostgreSQLContext).Assembly);
    }
}
