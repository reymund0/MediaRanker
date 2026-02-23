using Microsoft.EntityFrameworkCore;

public class PostgreSQLContext : DbContext
{
    public PostgreSQLContext(DbContextOptions<PostgreSQLContext> options) : base(options) { }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        return;
    }
}