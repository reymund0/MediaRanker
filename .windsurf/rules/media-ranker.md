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

## Repo Layout (minimal)

- `MediaRankerFrontend/` — Next.js app (`src/app`, `src/lib`)
- `MediaRankerServer/` — ASP.NET Core API (`Controllers`, `Services`, `Data`, `Migrations`)
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

- Seed helpers live in `MediaRankerServer/Data/Seeds` (for example `SystemTemplates`, `SeedUtils`).
- Seed IDs are static and negative to indicate system-seeded rows.
- Canonical system user id: `SeedUtils.SystemUserId = "system"`.
- Migrations should reference seed helpers/constants instead of duplicating literals.
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
- Navbar behavior:
  - Hide on `/auth/*`
  - Show on non-auth routes
- Alert behavior is provider-based (`useAlert`) with a single active alert.
- Keep existing app/provider composition patterns in `src/app/layout.tsx`.

---

## Optional AI Reference Docs

Use these when a task needs deeper context:

- `docs/ai/backend-seeding.md`
- `docs/ai/frontend-conventions.md`
- `docs/ai/dev-commands.md`