---
trigger: always_on
---

# MediaRanker — AI Editor Context (Core)

This is the always-on, high-signal guidance for AI edits in this repository.
Use optional docs under `docs/conventions/` for deeper details.

---

## Project Snapshot

- App type: personal media reviewing/ranking web app.
- Current status: auth is implemented; initial template/field persistence and seed migrations exist.
- Authorization model: any authenticated user (no roles/scopes yet).
- Architecture: Modular Monolith (Option A - feature folders in single project).

## Repo Layout (updated)

- `MediaRankerFrontend/` — Next.js app (`src/app`, `src/lib`)
- `MediaRankerServer/` — ASP.NET Core API
  - `Modules/` — Feature-based modules (Templates, Media, Reviews, Test)
    - Each module keeps persistence concerns under `Data/`:
      - `Data/Entities/` — EF entities + configurations
      - `Data/Views/` — keyless read-model/view entities + SQL view artifacts
      - `Data/Seeds/` — module-owned seed SQL artifacts
  - `Shared/` — Cross-cutting concerns (Exceptions, Extensions, Events)
  - `Data/` — Data access (shared PostgreSQLContext)
  - `Migrations/` — EF Core Migrations (kept migration-compatible)
  - `MediaRankerServer.IntegrationTests/` — PostgreSQL-backed endpoint tests (Testcontainers)
  - `MediaRankerServer.UnitTests/` — Isolated logic tests (Moq)
- `.windsurf/rules/media-ranker.md` — this core AI context file
- `docs/conventions/` — optional, non-always-on AI reference docs

Do not edit build artifacts:
- `MediaRankerFrontend/.next/`
- `**/bin/`
- `**/obj/`

---

## Critical Backend Rules

- Keep backend changes migration-driven (EF Core + PostgreSQL).
- Default controller posture is authenticated; use `[AllowAnonymous]` intentionally.
- Keep controllers thin; place business/domain logic in services.
- Keep persistence concerns in entities/configurations/migrations.
- Keep FK constraints within module-owned tables only. Do not add cross-module DB foreign keys.
- For cross-module references, persist scalar IDs and add explicit indexes on those reference columns for read/query performance.
- Prefer incremental refactors over broad rewrites.

### Hosted Services (Scheduled Jobs)

- Use `IHostedService`/`BackgroundService` for recurring server-side jobs (for example, daily cleanup) instead of controller-triggered execution.
- Keep hosted services orchestration-focused: schedule/timing, scoped dependency resolution, logging, and cancellation handling.
- Resolve scoped services inside a created scope (`IServiceScopeFactory`) per run; do not capture scoped dependencies directly in singleton hosted services.
- Keep domain/business rules in module services and event handlers; hosted services should invoke existing services/events rather than duplicate domain logic.
- Make job behavior configuration-driven (`IOptions<T>`), including an enable/disable flag and thresholds/timing settings.
- Wrap each run in exception handling, log start/finish with counts, and continue scheduling subsequent runs unless cancellation is requested.
- For large external dataset ingestion, use a provider class with a callback-driven batch pattern: `RunBatchImportAsync(Func<List<TRow>, CancellationToken, Task> batchHandler, ct)`. The provider owns download/decompression/parse; the caller supplies the batch insert logic. See `docs/conventions/backend-conventions.md` for the full pattern.

### File Upload Lifecycle (Files Module)

- Uploads are module-driven and two-phase:
  1. Frontend requests an upload URL from a feature module endpoint.
  2. Module validates and calls `IFileService.StartUploadAsync(...)` to create upload state + pre-signed URL.
  3. Frontend uploads file directly using the pre-signed URL.
  4. Frontend calls module endpoint to complete upload.
  5. Module validates and calls `IFileService.FinishUploadAsync(...)` (`Uploading` -> `Uploaded`).
  6. Frontend submits module save/upsert with `uploadId` attached.

- `Uploaded` files are temporary and may be deleted by daily cleanup.
- Feature modules must call `IFileService.MarkUploadCopiedAsync(uploadId, userId, ...)` and persist required `FileDto` data in module-owned entities during save flows.
- If a module does not copy upload data from the Files module, it risks losing file references during cleanup.
- Files module owns upload state lifecycle (`Uploading`, `Uploaded`, `Copied`, `Deleted`); feature modules own domain validation and when uploads are attached to domain models.

### Seed + Migration Conventions

- Seed artifacts live under module `Data/Seeds` folders (e.g., `MediaRankerServer/Modules/Templates/Data/Seeds/SeedSystemTemplates.sql`).
- Seed IDs are static and negative to indicate system-seeded rows.
- Keep system-owned identity values centralized in seed artifacts/migrations instead of scattering literals across services/controllers.
- Migrations should reference seed artifacts/constants instead of duplicating literals.
- Treat existing migrations as immutable history; create a new migration for seed changes.
- Keep `Down` deterministic and verify FK behavior (cascade/restrict).

### EF CLI Quick Rules

- Apply latest: `dotnet ef database update`
- Roll back: `dotnet ef database update <TargetMigration>` (or `0`)
- Invalid commands: `dotnet ef database rollback`, `dotnet ef database upgrade`

---

## Critical Frontend Rules

- Theme is centralized and currently dark mode.
- Prefer theme tokens over one-off hardcoded colors.
- Custom components follow a Base/Form pattern (MUI-based wrappers): create/extend Base components for shared behavior and Form variants for controlled form usage. See `docs/conventions/frontend-conventions.md` for more details.
- Navbar behavior:
  - Hide on `/auth/*`
  - Show on non-auth routes
- Alert behavior is provider-based (`useAlert`) with a single active alert.
- Keep existing app/provider composition patterns in `src/app/layout.tsx`.

### Testing Conventions

- **Unit Tests**: Use xUnit + FluentAssertions + Moq. Focus on pure logic, mapping, and extensions.
- **Integration Tests**: Use Testcontainers (PostgreSQL + LocalStack for AWS-backed flows) + Respawn.
  - Inherit from `IntegrationTestBase`.
  - Use `Fixture.CreateClient()` for authenticated requests (defaults to `test-user-1`).
  - Tests are migration-driven; the fixture handles schema migration and data seeding.
  - Use LocalStack for AWS-backed integration behavior (e.g. S3).
  - Hybrid configuration: `AWS:Region` is set via host-level in-memory collection; other settings come from `appsettings.Integration.json`.
  - Assert ProblemDetails shape for error paths.

---

## Additional AI Reference Docs

Use these when a task needs deeper context:

- `docs/conventions/backend-testing.md`
- `docs/conventions/backend-seeding.md`
- `docs/conventions/backend-conventions.md`
- `docs/conventions/frontend-conventions.md`
- `docs/conventions/dev-commands.md`
