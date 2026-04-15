using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaRankerServer.UnitTests.Shared;

public static class TestDbContextFactory
{
    public static PostgreSQLContext Create()
    {
        var options = new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new PostgreSQLContext(options);
        context.Database.EnsureCreated();

        return context;
    }
}
