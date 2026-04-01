# Dev Commands Reference

This document captures common local commands for MediaRanker.

## Local Run

- Frontend (from `MediaRankerFrontend`):
  - `pnpm run dev`
- Backend (from `MediaRankerServer`):
  - `dotnet run`

## EF Core Migrations

From `MediaRankerServer`:

- List migrations:
  - `dotnet ef migrations list`
- Apply latest migration:
  - `dotnet ef database update`
- Roll back to a target migration:
  - `dotnet ef database update <TargetMigration>`
- Revert all migrations:
  - `dotnet ef database update 0`

Invalid EF commands to avoid:

- `dotnet ef database rollback`
- `dotnet ef database upgrade`
