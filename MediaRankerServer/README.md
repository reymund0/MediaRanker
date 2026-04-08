# MediaRankerServer

ASP.NET Core Web API built as a **Modular Monolith** with feature-based modules, a shared PostgreSQL database, and in-process domain events.

## Libraries

| Library | Used For | Why I chose this? |
|---|---|---|
| **EF Core + Npgsql** | ORM and PostgreSQL database provider | For a code-first approach to PostgreSQL interactions and support for DI while improving my knowledge of EF Core |
| **FluentValidation** | Declarative request validation with auto-validation pipeline | Provides a clean and consistent way to validate requests in APIs and services |
| **MediatR** | In-process domain event publishing and handling between modules | Lightweight .Net friendly library to implement events without the need for an external service |
| **Serilog** | Structured logging to console and rolling file sinks | Easy to configure and provides structured logging in API flows and background jobs |
| **AWSSDK.S3** | S3 operations — pre-signed URL generation, object deletion | Industry standard for S3 interactions |
| **AWSSDK.Extensions.NETCore.Setup** | AWS service registration in the DI container | To simplify configuring AWS services with DI |
| **JWT Bearer Authentication** | Middleware for validating JWT tokens on API requests | To extract and validate Cognito-issued JWT tokens from requests |

## Modules

Each module owns its own controllers, services, entities, contracts, event handlers, and background jobs. This allows feature logic to remain self-contained while enabling cross-module communication via service calls and domain events.

### Templates

Defines **review templates** and their ordered fields. Templates provide the scoring structure that reviews are built against.

- Publishes `TemplateFieldsDeletedEvent` when template fields are removed, allowing downstream modules to react.
- Publishes `TemplateDeletedEvent` when a template is deleted.
- Includes SQL seed data for system-owned templates (negative IDs).

### Media

Manages **media entries** and **media types** (movies, video games, etc.).

- Owns the cover image upload flow — coordinates with the Files module for pre-signed URL generation and upload lifecycle.
- Publishes `MediaDeletedEvent` when media is deleted.
- Publishes `FileDeletedEvent` for invalid media cover uploads and explicit cover-file deletion flows.
- Includes SQL seed data for system-owned media types (negative IDs).
- Includes an **IMDB import pipeline** that downloads, parses, and stages the IMDB title dataset into an `imdb_imports` table:
  - `ImdbTsvProvider` — streams and parses the `.tsv.gz` dataset; supplies batches to the caller via a `RunBatchImportAsync` callback
  - `ImdbImportService` — wires the provider to raw SQL batch inserts with `ON CONFLICT (tconst) DO NOTHING` dedup
  - `ImdbImportJob` — `BackgroundService` that runs the import on a configurable daily schedule
  - Disabled by default; enable via `Media:ImdbImport:Enabled` in config

### Reviews

User **reviews** scored against a template's fields. A review links a user, a media entry, and a template together.

- Handles `TemplateFieldsDeletedEvent` to recalculate review scores and clean up orphaned review fields when template fields are removed.
- Handles `TemplateDeletedEvent` to delete reviews linked to a deleted template.
- Handles `MediaDeletedEvent` to delete reviews linked to deleted media.

### Files

Manages the **file upload lifecycle** — upload state tracking, pre-signed URL generation, and background cleanup.

- Two-phase upload: API creates upload state and returns a pre-signed S3 URL; frontend uploads directly to S3; API confirms completion.
- Background cleanup job (`FileUploadCleanupJob`) removes stale uploads on a configurable schedule.
- Feature modules must mark uploads as copied during their save flows to prevent cleanup.
- Publishes `FileDeletedEvent` from cleanup when stale uploaded files are being removed.
- Handles `FileDeletedEvent` to perform storage cleanup (for example, deleting S3 objects).

## Configuration

Copy `appsettings.Development.json.template` to `appsettings.Development.json` and fill in the required values.

`appsettings.Development.json` is gitignored — never commit credentials.

## Shared Layer

Cross-cutting concerns used by all modules:

| Component | Purpose |
|---|---|
| `PostgreSQLContext` | Shared EF Core DbContext for all module entities |
| `DomainException` | Expected domain exceptions with user friendly messages mapped to ProblemDetails responses |
| `ProblemDetailsExtensions` | Centralized exception-to-ProblemDetails mapping to standardized API error responses and controller exception handling |
| `AuthenticationExtensions` | Cognito JWT Bearer configuration |
| `ClaimsPrincipalExtensions` | Helper methods for extracting user identity from claims |
| `ITimestampedEntity` | Interface for automatic `CreatedAt`/`UpdatedAt` tracking |
