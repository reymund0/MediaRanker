using FluentAssertions;
using MediaRankerServer.Modules.Media.Data;
using MediaRankerServer.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MediaRankerServer.UnitTests.Modules.Media;

public class ImdbSqlLoggingTests
{
    [Fact]
    public async Task ImportFailureLogContainsOnlyStageCountAndCategory()
    {
        await using var db = new PostgreSQLContext(new DbContextOptionsBuilder<PostgreSQLContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var logger = new CapturingLogger<ImdbImportSqlProvider>();
        var provider = new ImdbImportSqlProvider(db, logger);

        var run = () => provider.ImportBasicsAsync([
            new ImdbTsvRow("tt0000001", "movie", "secret title", "secret title", false, 2020, null, null, null, "raw-secret")
        ], CancellationToken.None);

        await run.Should().ThrowAsync<InvalidOperationException>();
        logger.Messages.Should().ContainSingle();
        logger.Messages.Single().Should().NotContain("secret title").And.NotContain("raw-secret").And.NotContain("INSERT");
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
