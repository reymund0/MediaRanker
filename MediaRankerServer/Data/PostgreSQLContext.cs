using Microsoft.EntityFrameworkCore;
using MediaRankerServer.Data.Entities;

public class PostgreSQLContext : DbContext
{
    public PostgreSQLContext(DbContextOptions<PostgreSQLContext> options) : base(options) { }

    public DbSet<User> Users { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Username = "admin", Email = "admin@admin.com", Password = "password" }
        );
    }
}