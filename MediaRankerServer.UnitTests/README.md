# MediaRanker Tests

Shared documentation for both test projects in the solution.

## Shared Libraries

| Library | Used For | Why I chose this? |
|---|---|---|
| **xUnit** | Test framework — test execution, and assertions | Easy to use and widely adopted framework |
| **FluentAssertions** | Expressive assertion syntax for readable test expectations | Commonly used library and easy to read assertions |

## Unit Tests (`MediaRankerServer.UnitTests/`)

Isolated logic tests — pure service behavior, mapping, and extensions with no external dependencies.

| Library | Used For | Why I chose this? |
|---|---|---|
| **Moq** | Mocking interfaces and dependencies for isolated unit testing | Convenient and widely used mocking library |
| **EF Core InMemory** | Lightweight in-memory database provider for data-layer unit tests | So I can test EF Core interactions without the overhead of spinning up a real database (that's done in integration tests) |

## Integration Tests (`MediaRankerServer.IntegrationTests/`)

Full-stack endpoint tests running against real infrastructure via containers.

| Library | Used For | Why I chose this? |
|---|---|---|
| **Testcontainers (PostgreSQL)** | Spins up a real PostgreSQL container per test run for migration-driven schema testing | Integration tests can test the Data Layer instead of just mocking. Also has the added benefit of testing the actual database schema and migrations |
| **Testcontainers (LocalStack)** | Spins up a LocalStack container to emulate AWS services in tests (currently only S3) | So I can test AWS interactions without deploying and managing a real test environment |
| **Respawn** | Resets database state between tests while preserving seeded data | Convenient way to reset the database state between tests and preserve seeded data |
| **WebApplicationFactory / TestHost** | Hosts the API in-process for HTTP-level integration testing | Allows for testing API endpoints without spinning up a full web server |
