# MediaRankerServer

ASP.NET Core Web API built as a **Modular Monolith** with feature-based modules, a shared PostgreSQL database, and in-process domain events.

## Libraries

| Library | Used For | Why I chose this? |
|---|---|---|
| **EF Core + Npgsql** | ORM and PostgreSQL database provider | I haven't used EF Core in a while and wanted to practice rather than use Dapper |
| **FluentValidation** | Declarative request validation with auto-validation pipeline | Developer friendly and commonly used validation library |
| **MediatR** | In-process domain event publishing and handling between modules | Convenient to implement in a dotnet project. Eventually I consider using AWS EventBridge for cross-module events |
| **Serilog** | Structured logging to console and rolling file sinks | Easy to configure and has great support for structured logging |
| **AWSSDK.S3** | S3 operations — pre-signed URL generation, object deletion | |
| **AWSSDK.Extensions.NETCore.Setup** | AWS service registration in the DI container | |
| **JWT Bearer Authentication** | Validating Cognito-issued JWT tokens on API requests | |

## Modules

Each module follows a consistent internal structure: `Controllers/`, `Services/`, `Entities/`, `Contracts/`, `Events/`, `EventHandlers/`, `Seeds/`, `Jobs/`, and `Data/`.

### Templates

Defines **review templates** and their ordered fields. Templates provide the scoring structure that reviews are built against.

- Publishes `TemplateFieldsDeletedEvent` when template fields are removed, allowing downstream modules to react.
- Includes SQL seed data for system-owned templates (negative IDs).

### Media

Manages **media entries** and **media types** (movies, video games, etc.).

- Owns the cover image upload flow — coordinates with the Files module for pre-signed URL generation and upload lifecycle.
- Includes SQL seed data for system-owned media types (negative IDs).

### Reviews

User **reviews** scored against a template's fields. A review links a user, a media entry, and a template together.

- Handles `TemplateFieldsDeletedEvent` to recalculate review scores and clean up orphaned review fields when template fields are removed.

### Files

Manages the **file upload lifecycle** — upload state tracking, pre-signed URL generation, and background cleanup.

- Two-phase upload: API creates upload state and returns a pre-signed S3 URL; frontend uploads directly to S3; API confirms completion.
- Background cleanup job (`FileUploadCleanupJob`) removes stale uploads on a configurable schedule.
- Feature modules must mark uploads as copied during their save flows to prevent cleanup.

## Configuration

Copy `appsettings.Development.json.template` to `appsettings.Development.json` and fill in the required values.

`appsettings.Development.json` is gitignored — never commit credentials.

## Shared Layer

Cross-cutting concerns used by all modules:

| Component | Purpose |
|---|---|
| `PostgreSQLContext` | Shared EF Core DbContext for all module entities |
| `DomainException` | Expected domain exceptions with user friendly messages mapped to ProblemDetails responses |
| `ProblemDetailsExtensions` | Centralized exception-to-ProblemDetails mapping to standardized API error responses along with DRY-ing controller code by removing repetitive try/catch blocks |
| `AuthenticationExtensions` | Cognito JWT Bearer configuration |
| `ClaimsPrincipalExtensions` | Helper methods for extracting user identity from claims |
| `ITimestampedEntity` | Interface for automatic `CreatedAt`/`UpdatedAt` tracking |
