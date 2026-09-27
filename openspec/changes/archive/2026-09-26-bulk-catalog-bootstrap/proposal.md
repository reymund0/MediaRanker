## Why

The accepted cover-art implementation can resume IGDB ingestion, but its daily default scans at most 1,000 game rows per scheduled run and does not bound staging admission. A practical initial catalog needs explicit, observable, finite bootstrap execution and measured database/provider limits before larger imports.

## What Changes

- Add an opt-in initial bootstrap mode to the existing backend import jobs, with finite session and work-unit limits, progress summaries, cancellation, and deliberate restart. Preserve normal scheduled refresh after successful bootstrap.
- Keep the providers distinct: IMDb bulk dataset replay for movie/TV metadata, and IGDB cursor-based game bootstrap followed by incremental refresh. Preserve existing title selection, vote thresholds, and release rules.
- Bound IGDB HTTP attempts, elapsed time, pages, and staging admission separately. Reuse its durable lease, fixed bounds, atomic page/cursor commits, shared client throttling, and imported cover references.
- Make IMDb stage failures explicit; require complete successful ingestion before cleanup/domain loading. Bound downloads and database work, and document idempotent replay rather than promising a durable IMDb resume cursor.
- Preserve demand-driven artwork and server-side credentials. Bulk metadata must not enqueue a catalog-wide artwork refresh, call TMDB per imported title, download image bytes, or change browser provider access.
- Establish reproducible local/fake-provider measurements and data/performance review gates before selecting a larger operating profile. Fix only demonstrated reliability/performance problems needed to meet those gates.

## Capabilities

### New Capabilities

- `bulk-catalog-bootstrap`: Explicit finite bootstrap sessions, provider-specific recovery, bounded admission, progress reporting, and measured readiness for initial catalog ingestion.

### Modified Capabilities

None in the main specification tree, which is empty. This capability builds on the accepted, unarchived `automatic-cover-art` change without editing or reopening its specifications or 33 completed tasks.

## Authorized closeout amendment — 2026-09-26

The user subsequently authorized review fixes, commits, specification sync/archive and a PR, while deferring scheduled-import activation. They also approved integrating upstream MR-51 (`d2d2e8b`), which replaces the media-types table with canonical string types. This expands integration to affected API/frontend contracts, import/artwork queries, fixtures and migration compatibility. The explicit migration-history exception permits correcting `RemoveMediaTypesTable` and `AutomaticCoverArt` so fresh and already-imported histories preserve existing type values and build the correct review view. Unknown mappings must stop migration rather than silently lose data. Verify these paths on disposable databases; the loaded local database is outside this closeout's write scope. The original no-commit/no-PR/no-schema limits below are superseded only for this approved amendment.

## Impact

- Backend: existing Media import jobs/options/services, IMDb parser/SQL providers, IGDB client/limiter and staging loader; artwork demand registration only if representative measurements require a bounded batching fix.
- Verification: focused fake HTTP, hosted-job, and isolated PostgreSQL tests; a reproducible measurement harness and sanitized results; operator documentation for start, stop, resume, and interpreting progress.
- Baseline: checkpoint `346ccc6c8e446b69cd235de3ef9e5a87904e63c6` on isolated branch `codex/bulk-catalog-bootstrap`. The original app, database, secrets, and private evidence remain untouched.
- No new schema, dependencies, distributed scheduler, public control API, or bootstrap UI is proposed. Any subsequently demonstrated need for those changes requires a scoped amendment before implementation.
- Non-goals: database reset, IMDb game imports, changing selection policy, mass artwork backfill, image uploads, multi-process rate-limit coordination, production deployment, commits/PRs, or a live bulk run authorized merely by this proposal.
