# Backend Seeding and Migration Reference

This document contains non-always-on details for how MediaRanker handles system seed data.

## Source of Truth

- Seed helpers are under `MediaRankerServer/Data/Seeds`.
- Current helpers include:
  - `SystemTemplates`
  - `SeedUtils`

## Seed Identity Rules

- Seed IDs are static constants.
- Seed IDs use negative numeric values to avoid collision with DB-generated positive IDs.
- System-owned rows use `SeedUtils.SystemUserId = "system"`.

## Migration Pattern

- Seed migrations call helper methods/constants (for example `SystemTemplates.GenerateSeeds()`).
- Avoid copying literal IDs and values directly into migrations.
- Keep `Up` and `Down` deterministic.

## Rollback Safety

- When simplifying `Down`, verify FK behavior (cascade/restrict) first.
- If deleting by system user scope, ensure this aligns with current domain rules.
- Prefer explicit, predictable delete behavior for seeded rows.

## Change Policy

- Treat committed migrations as immutable history.
- If seed values change after migration commit, create a new migration.
- Do not rewrite prior migration behavior in place unless you intentionally reset migration history.
