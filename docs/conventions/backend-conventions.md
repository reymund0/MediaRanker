# Backend Conventions

## Project Structure (Modular Monolith)

The server is organized into feature modules under `MediaRankerServer/Modules/`.

- **Module Structure**: Each module contains its own Controllers, Services, Contracts, and Data concerns.
- **Persistence Layout**: Keep module persistence artifacts under `Modules/<Module>/Data/`:
  - `Data/Entities` for EF entities/configurations
  - `Data/Views` for keyless read-model/view entities and view SQL artifacts
  - `Data/Seeds` for module-owned seed SQL
- **Shared Infrastructure**: `MediaRankerServer/Shared/` contains cross-cutting concerns like `DomainException` and common extensions.
- **Module Registration**: Each module has a `<Name>Module.cs` registration class. Add new modules to `Program.cs` via `builder.Services.Add<Name>Module()`.
- **Inter-Module Communication**: Use MediatR for in-process events instead of direct service injection to maintain decoupling.

## Foreign Key + Index Pattern
- Keep DB foreign keys within module-owned tables only.
- Do not create cross-module foreign keys.
- For cross-module references, persist scalar IDs (for example, `MediaId`, `TemplateId`) and create explicit indexes on those columns for query performance.

## ProblemDetails responses
- Every non-OK HTTP response must return RFC 7807 ProblemDetails JSON with `type`, `title`, `status`, `detail`, and optional `instance`/extensions.
- Controllers/services should populate `detail` with the user-facing string; frontend hooks only surface this field while logging the full object.
- Include any correlation metadata (e.g., `errorId`) inside `problemDetails.extensions` for easier troubleshooting.

## Serilog logging
- Structured logging is handled by Serilog; prefer `ILogger` templates so properties stay queryable.
- Domain/validation failures: log at `Warning` with the domain-specific `type` and user context when available.
- Unexpected exceptions: log at `Error` with `errorId`, then include the same identifier in the ProblemDetails payload so clients can reference it when reporting issues.

## Validation
- Request validation lives in FluentValidation `AbstractValidator<T>` classes (e.g., `TemplateUpsertRequestValidator`).
- Keep lightweight validators and mappers colocated with their related request/contract class (same file or same folder) so discovery stays straightforward.
- Services/controllers resolve `IValidator<T>` via DI and throw `DomainException` with the existing `type` values (e.g., `template_validation_error`) when validation fails so ProblemDetails stays consistent.

## Hosted Services (Scheduled Background Jobs)
- Use `IHostedService`/`BackgroundService` for recurring server-side jobs (for example, daily cleanup) instead of controller-triggered execution.
- Keep hosted services orchestration-focused: schedule/timing, scoped dependency resolution, logging, and cancellation handling.
- Resolve scoped dependencies per run via `IServiceScopeFactory`; do not inject scoped services directly into hosted service constructors.
- Keep business/domain logic in module services and event handlers; hosted services should invoke those abstractions rather than duplicate rules.
- Make job behavior configuration-driven with `IOptions<T>` (for example: enabled flag, thresholds, schedule-related settings).
- Wrap each run in exception handling, log start/finish plus success/failure counts, and continue the schedule unless cancellation is requested.

## External Data Ingestion (Large Dataset Imports)

For importing large external datasets (e.g., IMDB TSV files), use a callback-driven batch provider pattern to keep I/O and persistence cleanly separated:

- **Provider class** (e.g., `ImdbTsvProvider` in `Modules/<Module>/Data/`) owns I/O with external data set. Supplies batches of data to the caller via a callback. See: `RunBatchImportAsync(Func<List<TRow>, CancellationToken, Task> batchHandler, CancellationToken ct)`.
- **Service class** (e.g., `ImdbImportService`) owns:
  - Wiring the provider to actual persistence
  - Calling module-owned SQL providers for bounded staging and cleanup units
  - Tracking invocation totals across callbacks; retain the stream owner while creating fresh database scopes per unit
  - Aborting on any failed batch or invalid required feed data; log safe stage/count/error categories without SQL, raw rows or exception payloads
- **Staging tables** deduplicate by external identity. IMDb ratings refresh their timestamps; cleanup uses one database-clock cutoff captured before the ratings feed. No cleanup or domain loading may begin until ratings, basics and episodes have all reached validated gzip EOF and committed every batch.
- **`BackgroundService` job** for regular syncs of datasets (if dataset is small enough)
- Keep import and load responsibilities separate:
  - Import providers/services move external rows into staging tables.
  - Load providers/services transform staged rows into domain tables.
- For staged-to-domain loads, prefer module-owned raw SQL providers for large set-based operations. Keep sequencing in the service when one load depends on another, such as Series before Seasons before Episodes.
- Make load operations idempotent where practical with `INSERT ... ON CONFLICT DO UPDATE`, and log affected counts as affected rows rather than inserted-only counts.
- IMDb episode staging conflicts update parent, season/episode numbers, raw data and timestamp. Replay affected counts include updates, so skipped counts can be zero. Domain loads refresh numeric hierarchy fields and relink from staging. Season batches select final parent/season groups and aggregate every episode belonging to each selected group; exclude season -1 before limiting the batch.
- After episode loading, bounded cleanup removes IMDb TV episodes in unnumbered seasons, then empty seasons and their newly emptied series. Preserve reviewed episodes and reviewed series, log each batch, advance by scanned candidates (including protected rows), and apply `MaxStatementSeconds`. Season and emptied-parent removal share a transaction. Never run the calibrated import without operator approval.

## TV hierarchy and review targets

- Series and episodes are reviewable; seasons are navigation only. `SeasonNumber` and `EpisodeNumber` are nullable numeric ordering fields; zero is valid. Hide unnumbered seasons and series containing only unnumbered seasons on read, while retaining hand-added series with no seasons. Search matches series names only.
- A review has exactly one scalar target: `MediaId` or `MediaCollectionId`. Collection targets must be visible TV series and use TV templates, as episodes do. Keep the database XOR constraint and filtered per-user uniqueness; do not add cross-module foreign keys.
- Keep hierarchy context in the Reviews `review_details` view artifact. Freeze SQL inside migrations so future view edits do not alter old migrations. The hierarchy migration's Down refuses rollback while series reviews exist.
- Series removal counts reviews across users and deletes the hierarchy in a transaction with `SeriesDeletedEvent`; the Reviews handler deletes associated reviews before commit.
- IMDb replay has finite HTTP, compressed/decompressed byte, temporary-disk, row, line, statement and whole-invocation limits. A failure requires full feed replay, not a byte/row resume. Live activation requires explicit operator confirmation of a calibrated finite profile; test defaults cannot establish full-feed readiness.
- Callback batches also have an aggregate raw-character limit, so wide rows flush before reaching the row cap. IMDb providers resolve a separate scoped context through the `imdb` key with EF logging disabled: raw SQL contains provider values. Their own stage/count/category logs remain enabled, and ordinary contexts retain their existing diagnostics. Do not replace this registration with the ordinary context or log caught exception payloads.
- For long-running bulk SQL, set command timeout around the operation and reset it in `finally` so incidental queries on the same context are not affected.

## File Upload Lifecycle (Module + Files Module)

Media artwork is automatic and does not use file uploads. The following lifecycle remains available to independent Files consumers.
- The upload flow is two-phase and module-driven:
  1. Frontend asks a module endpoint to start an upload.
  2. Module validates request and calls `IFileService.StartUploadAsync(...)` to get `UploadId` + pre-signed upload URL.
  3. Frontend uploads the binary directly to S3 using the pre-signed URL.
  4. Frontend calls a module endpoint to confirm upload completion.
  5. Module validates and calls `IFileService.FinishUploadAsync(...)`, transitioning `FileUploadState` from `Uploading` to `Uploaded`.
  6. Frontend later submits the module save/upsert request with the `uploadId` attached.

- Files in `Uploaded` state are temporary and may be removed by daily cleanup if never copied into module-owned data.
- Each module must copy file metadata it needs by calling `IFileService.MarkUploadCopiedAsync(uploadId, userId, ...)` during its own save flow, then persist the returned `FileDto` data in module-owned entities.
- If a module does not copy upload data out of the Files module, it risks losing the file reference during cleanup.
- The Files module owns upload state tracking (`Uploading`, `Uploaded`, `Copied`, `Deleted`); feature modules own business validation and when upload IDs become part of domain models.

## Automatic Artwork and IGDB Catalog

- IMDb stages and loads movies/TV only. IGDB stages game metadata and cover references, then admits released main games/remakes/remasters with no edition parent. Votes and artwork are not admission requirements. Future games remain staged and are reconsidered on successful scheduled runs.
- IGDB run bounds and the ascending-ID cursor live in `igdb_import_state`. Page staging and cursor advancement commit together. Preserve lease fencing, overlap replay, and source-version checks when changing import logic; a failed page must not advance the cursor.
- Catalog bootstrap is an explicit, finite process launch. `CatalogScheduleGate` holds both catalog schedules until the selected bootstrap succeeds; cap/fault/stop leaves both paused for that process. Artwork remains independent. Session counters are process-local; automatic restart is not a permitted bootstrap operating mode. See the bootstrap procedure in `dev-commands.md`.
- IGDB admission has independent row/batch/time limits and runs before upstream requests. Completed-scan bootstrap is admission-only, including when an incremental window is pending. Fresh-context conflict recovery is bounded; an admission-blocked result ends bootstrap and stops the scheduled IGDB job until remediation/restart.
- `MediaCover` stores a unique provider/lookup-kind/lookup-ID, optional asset reference, freshness, and durable request/lease state. Never store arbitrary URLs or provider image binaries in S3. Never accept provider lookup parameters from a client.
- Authorized Media/Reviews reads register work only for returned titles. The request path performs no provider HTTP calls. Artwork failure must not undo a saved review. Movies resolve by IMDb ID; IGDB games reuse fresh imported references. Seasons/episodes resolve through their series, with the association on the series rather than copied to every child.
- `ArtworkJob` waits two seconds after each processing batch by default. `ArtworkProcessor` atomically claims due requested rows and conditionally completes using claim/version guards. Expired dormant results do not activate HTTP work. Restart recovery uses expiring leases; retries are bounded and delayed.
- References expire after 30 days by default; no-image results after seven. Reads withhold expired URLs. Database-only maintenance removes expired TMDB IDs/paths at the start of each processing batch, even with upstream flags disabled. Cache options cap at 150 days. Slow provider calls or database work extend the interval between maintenance passes beyond the configured poll delay; it is not a fixed 60-second guarantee. After downtime, startup maintenance removes expired fields; run the application regularly when retaining provider metadata.
- Status is `ready`, `pending`, `missing`, `failed`, `disabled`, or `unsupported`; only `ready` includes a trusted HTTPS CDN URL. Fresh references can render with the network provider disabled. Missing credentials never leave new demand permanently pending.
- IGDB import and artwork share one in-process request limiter and token cache. The initial deployment assumes one application process per provider credential budget; coordinate an aggregate budget before scaling out.
- Keep HTTP credentials and response bodies out of logs. Authentication/429 cooldown applies across titles. Artwork processing skips a cooling provider while continuing the other provider; a local cooldown discovered after claiming does not spend a lookup attempt. Actual provider failures still count toward the retry limit. Provider clients and fake-handler tests live under Media; no provider SDK is required.
