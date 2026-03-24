---
trigger: always_on
---

# MediaRanker — AI Editor Context (Core)

This is the always-on, high-signal guidance for AI edits in this repository.
Use optional docs under `docs/ai/` for deeper details.

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
  - `Shared/` — Cross-cutting concerns (Exceptions, Extensions, Events)
  - `Data/` — Data access (shared PostgreSQLContext)
  - `Migrations/` — EF Core Migrations (kept migration-compatible)
  - `MediaRankerServer.IntegrationTests/` — PostgreSQL-backed endpoint tests (Testcontainers)
  - `MediaRankerServer.UnitTests/` — Isolated logic tests (Moq)
- `.windsurf/rules/media-ranker.md` — this core AI context file
- `docs/ai/` — optional, non-always-on AI reference docs

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
- Prefer incremental refactors over broad rewrites.

### Seed + Migration Conventions

- Seed artifacts live in `MediaRankerServer/Data/Seeds` (current seed file: `SeedSystemTemplates.sql`).
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
- Custom components follow a Base/Form pattern (MUI-based wrappers): create/extend Base components for shared behavior and Form variants for controlled form usage. See `docs/ai/frontend-conventions.md` for more details.
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

## Optional AI Reference Docs

Use these when a task needs deeper context:

- `docs/ai/backend-testing.md`
- `docs/ai/backend-seeding.md`
- `docs/ai/backend-conventions.md`
- `docs/ai/frontend-conventions.md`
- `docs/ai/dev-commands.md`
