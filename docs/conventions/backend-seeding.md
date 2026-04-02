# Backend Seeding and Migration Reference

This document contains non-always-on details for how MediaRanker handles system seed data.

## Source of Truth

- Seed artifacts are module-owned and live under `MediaRankerServer/Modules/<Module>/Data/Seeds`.
- Current seed artifacts:
  - `MediaRankerServer/Modules/Templates/Data/Seeds/SeedSystemTemplates.sql`
  - `MediaRankerServer/Modules/Media/Data/Seeds/SeedSystemMediaTypes.sql`

## Seed Identity Rules

- Seed IDs are static constants.
- Seed IDs use negative numeric values to avoid collision with DB-generated positive IDs.
- System-owned identity values should be kept in seed artifacts/migrations, not scattered across domain services.

## Migration Pattern

- Seed migrations should reference seed artifacts/constants rather than duplicating large literal payloads inline.
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
