# Backend Testing Conventions

This document provides guidance on how to write and maintain tests for the MediaRankerServer.

## Overview

The backend uses a two-tiered testing approach:
1.  **Unit Tests**: Fast, isolated tests for pure logic, mappers, and extension methods.
2.  **Integration Tests**: PostgreSQL-backed tests for API endpoints and service-to-database interactions.

---

## Unit Tests

- **Project**: `MediaRankerServer.UnitTests`
- **Stack**: xUnit, FluentAssertions, Moq.
- **Focus**:
    - Mapping logic (e.g., `TemplateMapper`).
    - Domain entity logic.
    - Custom extension methods (e.g., `ClaimsPrincipalExtensions`).
    - Service logic that can be easily isolated from the database.

---

## Integration Tests

- **Project**: `MediaRankerServer.IntegrationTests`
- **Stack**: xUnit, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers (PostgreSQL), Respawn.
- **Base Class**: All integration tests should inherit from `IntegrationTestBase`.

### Infrastructure

- **PostgresContainerFixture**: Manages the lifecycle of a real PostgreSQL container.
- **Respawn**: Cleans the database between tests while preserving the migration history.
- **TestAuthHandler**: Provides a fake authentication scheme. Use the header `X-Test-UserId` to simulate different users.

### Conventions

1.  **Authenticated Requests**:
    ```csharp
    using var client = CreateClient(); // Defaults to "test-user-1"
    // OR
    using var client = CreateClient("another-user-id");
    ```

2.  **Database Reset**:
    The database is automatically reset before each test by `IntegrationTestBase.InitializeAsync()`.

3.  **Seeding Data**:
    System data (MediaTypes, System Templates) is seeded automatically. Use helper methods within your test class to seed user-specific data for specific test scenarios.

4.  **ProblemDetails Assertions**:
    When testing error paths, assert the RFC 7807 `ProblemDetails` shape using `ProblemDetailsJson` utility.
    ```csharp
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    ProblemDetailsJson.GetType(payload).Should().Be("template_name_conflict");
    ```

---

## Running Tests

From the repository root:
- Run all tests: `dotnet test MediaRanker.sln`
- Run integration tests only: `dotnet test MediaRankerServer.IntegrationTests/MediaRankerServer.IntegrationTests.csproj`
