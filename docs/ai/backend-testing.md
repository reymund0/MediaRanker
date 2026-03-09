# Backend Testing Conventions

This document provides guidance on how to write and maintain tests for the MediaRankerServer.

## Overview

The project uses a **Tiered Testing Strategy** to balance confidence with maintainability:

1.  **Tier 1: Integration Tests (Smoke Suite)**: PostgreSQL-backed vertical slices for "Happy Path" verification.
2.  **Tier 2: Unit Tests (Logic Suite)**: Fast, isolated tests for business logic, validation, and edge cases.

---

## Tier 1: Integration Tests

- **Project**: `MediaRankerServer.IntegrationTests`
- **Stack**: xUnit, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing`, **PostgreSQL Testcontainers**, Respawn.
- **Focus**: Essential "Happy Path" CRUD operations to ensure the API, Database, and Migrations are correctly wired.
- **Base Class**: All integration tests must inherit from `IntegrationTestBase`.

### Database Isolation

- **Reset Strategy**: A hybrid approach is used to ensure test isolation:
    - **Respawn**: Automatically resets most tables between tests.
    - **Manual Cleanup**: Tables with mixed system-seeded data (negative IDs) and test data (positive IDs) are cleaned manually in `IntegrationTestBase.ResetMutableDataAsync()`.
- **Seeded Data**:
    - `media_types`, `templates`, and `template_fields` contain system-seeded data with **negative IDs**.
    - These rows persist across all tests.
    - Test-created rows (ID > 0) in these tables are deleted before and after each test.
- **Cleanup Timing**: Data is reset in both `InitializeAsync` (pre-test) and `DisposeAsync` (post-test) to maintain a consistent state.

---

## Tier 2: Unit Tests

- **Project**: `MediaRankerServer.UnitTests`
- **Stack**: xUnit, FluentAssertions, Moq, **EF Core In-Memory Provider**.
- **Focus**:
    - Service layer logic (e.g., `UpdateTemplateAsync` access checks).
    - Domain exception types and error mapping to ProblemDetails.
    - Validation rules (FluentValidation).
    - Mapping logic (e.g., `TemplateMapper`).
    - Custom extension methods.

---

## Running Tests

From the repository root:
- Run all tests: `dotnet test MediaRanker.sln`
- Run integration tests only: `dotnet test MediaRankerServer.IntegrationTests/MediaRankerServer.IntegrationTests.csproj`
- Run unit tests only: `dotnet test MediaRankerServer.UnitTests/MediaRankerServer.UnitTests.csproj`

### ProblemDetails Assertions

When testing error paths (usually in Unit Tests), assert the RFC 7807 `ProblemDetails` shape or the `DomainException.Type`.

```csharp
await act.Should().ThrowAsync<DomainException>()
    .Where(e => e.Type == "template_forbidden");
```
