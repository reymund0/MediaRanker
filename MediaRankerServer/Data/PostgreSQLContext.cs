using Microsoft.EntityFrameworkCore;
using MediaRankerServer.Data.Entities;

public class PostgreSQLContext : DbContext
{
    public PostgreSQLContext(DbContextOptions<PostgreSQLContext> options) : base(options) { }


    public DbSet<Media> Media => Set<Media>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<TemplateField> TemplateFields => Set<TemplateField>();
    public DbSet<RankedMedia> RankedMedia => Set<RankedMedia>();
    public DbSet<RankedMediaScore> RankedMediaScores => Set<RankedMediaScore>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PostgreSQLContext).Assembly);
    }
}