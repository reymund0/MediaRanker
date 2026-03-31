# MediaRanker Tests

Shared documentation for the unit and integration test projects in the solution.

## Testing Strategy

Because integration tests have higher overhead from spinning up containers for PostgreSQL and LocalStack, they are written primarily for critical path scenarios. Unit tests are used for varying business logic paths, error cases, and edge cases.

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
| **EF Core InMemory** | Lightweight in-memory database provider for data-layer unit tests | To test EF Core interactions without the overhead of spinning up a real database |

## Integration Tests (`MediaRankerServer.IntegrationTests/`)

Full-stack endpoint tests running against real infrastructure via containers.

| Library | Used For | Why I chose this? |
|---|---|---|
| **Testcontainers (PostgreSQL)** | Spins up a real PostgreSQL container per test run for migration-driven schema testing | Validates real database behavior, schema compatibility, and EF Core migrations instead of relying only on mocks |
| **Testcontainers (LocalStack)** | Spins up a LocalStack container to emulate AWS services in tests (currently only S3) | Integration tests can verify AWS interactions without deploying and managing a separate test environment |
| **Respawn** | Resets database state between tests while preserving seeded data | Provides repeatable DB cleanup between tests |
| **WebApplicationFactory / TestHost** | Hosts the API in-process for HTTP-level integration testing | Allows for testing API endpoints without spinning up a full web server |
