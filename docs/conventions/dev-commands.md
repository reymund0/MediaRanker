# Dev Commands Reference

This document captures common local commands for MediaRanker.

## Local Run

- Frontend (from `MediaRankerFrontend`):
  - `pnpm run dev`
- Backend (from `MediaRankerServer`):
  - `dotnet run`

## Local Development Test User

Browser automation using this identity is described in **Local browser tests** below.

Use the fixed `local-test-user` identity without a Cognito account. It is opt-in and available only in a Development backend with direct loopback requests and a localhost frontend. It is not a password or a production account; do not expose this development process through a public proxy.

Start the database from the repository root: `docker compose up -d postgres`. Apply migrations to the verified disposable local target with `dotnet ef database update --project MediaRankerServer/MediaRankerServer.csproj --startup-project MediaRankerServer/MediaRankerServer.csproj` (set the connection explicitly if your normal configuration targets something else).

Backend terminal, from repository root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:LocalTestAuth__Enabled = 'true'
dotnet run --project MediaRankerServer/MediaRankerServer.csproj --no-launch-profile --urls http://localhost:5157
```

Frontend terminal, from `MediaRankerFrontend`:

```powershell
$env:NEXT_PUBLIC_ENABLE_LOCAL_TEST_LOGIN = 'true'
$env:NEXT_PUBLIC_API_URL = 'http://localhost:5157'
pnpm run dev
```

Open `http://localhost:3000/auth/login` and select **Use local test user**. Refresh keeps this identity within the tab; Logout clears it. Reviews belong to the fixed local identity through the normal APIs. Real provider credentials are still required for live artwork discovery; this mode does not fabricate artwork. The local scheme is absent in Production and with its flag off. Normal Cognito login remains available. Keep both flags disabled for deployments and remove the terminal environment overrides to return to ordinary local login.

## Local browser tests

See [frontend testing](frontend-testing.md) for choosing real Docker-backed E2E coverage versus controlled browser mocks, and for test organization.

Use Node 20.9 or newer with pnpm available; this suite was verified with Node 24.20 and pnpm 12.6. From `MediaRankerFrontend`, install the frontend dependencies and Playwright Chromium:

```powershell
pnpm install
pnpm exec playwright install chromium
```

Run the real application journeys with Docker running, the .NET 9 SDK and `dotnet ef` available, and your developer API and Next.js processes stopped:

```powershell
pnpm test:e2e
```

The runner creates its own PostgreSQL 16 Compose project and ephemeral loopback database port, applies the existing migrations, and starts the API and Next.js on loopback. It does not use the ordinary Compose `pgdata` database. Tests run as the fixed local development identity, one at a time without retries. API fixtures and a post-test sweep clean up records, including records created through the UI. Failed cleanup prevents later journeys from running against contaminated state.

Run controlled frontend states without Docker or the API:

```powershell
pnpm test:browser
```

This starts Next.js and stubs application API responses and cover images. The cases cover failed-save retry, delayed stale responses, empty states, and artwork transitions/fallbacks. Unexpected API calls and external browser requests fail checks. Mocked responses provide frontend evidence, not evidence of server persistence or provider availability; keep payloads aligned with the DTOs and existing integration contracts.

Real journeys cover review persistence/ranking/deletion, scoring and cancellation, catalog search/pagination, and template duplication/reordering. Reload assertions exercise the real API and database.

Tests follow the application's domain layout. `tests/e2e/` contains `reviews/`, `media/`, and `templates/`; `tests/browser/` contains `reviews/` and `media/`. Each suite keeps cross-domain fixtures in `shared/`. Domain-specific helpers live beside their scenarios, while the Docker/process harness stays in `scripts/browser-tests/`. Add new scenarios under their owning domain; keep real-API fixtures separate from browser API mocks.

| Project / test name | Coverage |
| --- | --- |
| E2E: `real review lifecycle persists create edit rank and delete` | Create, reload, edit, ranking, cancelled and confirmed deletion |
| E2E: `chooser validation midpoint cancellation and local session` | Title chooser, query retention, reviewed-title exclusion, sole template, validation/rounding, cancellation, keyboard focus, narrow notes, reload and logout |
| E2E: `catalog search keeps the query and resets pagination on category change` | Search, no matches, category query retention and pagination reset |
| E2E: `built-in template duplicates reorders persists and drives review field order` | System template restrictions, duplicate, keyboard reorder, reload and saved review field order |
| Browser: `empty library and catalog offer their next actions without creating rows` | Empty states |
| Browser: `mock pagination slices a multi-page result and reports totals only when requested` | Fixture pagination and optional total-count contract |
| Browser: `failed review save keeps the draft and a successful retry creates one review` | Error, draft retention and retry |
| Browser: `an acknowledged review edit survives a delayed stale library read` | Delayed response after edit |
| Browser: `pending artwork refreshes to a fulfilled local image and missing artwork has a fallback` | Pending, ready and missing covers |
| Browser: `failed image loads and failed artwork status both retain a usable fallback` | Image and provider failures |

Run the first journey alone, or open a retained report (replace `<run-id>` with the printed directory name):

```powershell
pnpm test:e2e --grep "real review lifecycle persists create edit rank and delete"
pnpm exec playwright show-report scripts/browser-tests/runs/<run-id>/playwright-report
```

To watch a journey in a visible Chromium window with Playwright Inspector, run this in PowerShell from `MediaRankerFrontend`. Use **Resume** to run or **Step over** to advance through actions:

```powershell
$env:PWDEBUG = '1'
try {
  pnpm test:e2e --grep "real review lifecycle persists create edit rank and delete"
} finally {
  Remove-Item Env:PWDEBUG -ErrorAction SilentlyContinue
}
```

This keeps the isolated Docker stack and cleanup harness. The runner's twenty-minute Playwright deadline still applies while paused in Inspector. Omit `--grep` to watch all E2E journeys, or use `pnpm test:browser` inside the same block for mocked scenarios.

Run existing unit tests separately using Node 24 (these tests import TypeScript directly):

```powershell
node --test tests/unit/**/*.test.mjs
```

Each browser run prints its artifact directory under `scripts/browser-tests/runs/`. It retains runner/application logs, bounded PostgreSQL logs (`postgres.log`), the HTML report, and failure traces/screenshots after teardown. Owned child PIDs and the exact Docker project cleanup command are logged at startup. Compose startup and migration commands have five-minute deadlines; Playwright has a twenty-minute deadline. E2E fixture cleanup evidence is recorded in `fixture-cleanup.jsonl`; `fixture-cleanup-failed.json` records a failed cleanup. Generated run artifacts are ignored by Git.

The runner refuses an existing `.next/dev/lock` and an active developer API from this checkout. On Windows it also conservatively refuses ambiguous `dotnet run`/`dotnet watch` and IIS Express processes; stop those hosts before running E2E. Do not delete a lock belonging to a running server. Tests share the generated Next cache; restart your ordinary developer server with its normal environment after testing. API file logs belong to the run directory. Test child processes explicitly disable imports, upstream artwork providers, bootstrap, and Files cleanup; the existing artwork worker still performs database housekeeping against the disposable database. The Test module registers services, not another background job.

On normal completion or handled interruption, the runner stops its processes and removes only its own Compose project and volumes. After an uncatchable termination, inspect the run log for its exact project name and verify that its owned processes have stopped before removing that project with `docker compose -f scripts/browser-tests/compose.e2e.yml -p <exact-run-project> down --volumes`. Never substitute the development Compose project or use a broad Docker prune. Diagnose a reported leftover process by its run log and PID; do not stop an unrelated developer process.

These are Chromium tests of `next dev --webpack` and the localhost-only development identity. The test runner selects Webpack because repeated Turbopack launches encountered a Google-font query compilation error; ordinary development scripts and application assets are unchanged. They do not certify production builds, Cognito login, account isolation, live media providers, Turbopack behavior, or other browsers. Package/browser installation and the app's existing Google font compilation may require network access; a font/startup failure must be diagnosed rather than reported as a passing browser check.

## Docker Compose

- Local Postgres is configured with `max_wal_size=4GB` in `docker-compose.yml` to support large IMDB import/load operations.

## EF Core Migrations

The catalog-bootstrap branch integrates MR-51's canonical media types with the automatic-cover-art schema. Its approved compatibility correction maps all six legacy types before removing their IDs and handles databases where automatic cover art was already applied before MR-51. Unknown type mappings fail rather than discard data. Back up the intended database before applying this combined history; local verification uses disposable databases and does not upgrade the loaded application database. A database that already ran the original destructive MR-51 migration cannot recover lost type values from this correction alone.

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

## Automatic Cover Art Setup

All new network integrations are disabled in committed configuration. From `MediaRankerServer`, use ASP.NET user secrets locally or environment settings in the host; never commit credential values. Configure these keys outside source control:

| Setting | Purpose/default |
| --- | --- |
| `Media:Igdb:ClientId`, `Media:Igdb:ClientSecret` | Twitch application credentials for IGDB |
| `Media:Igdb:ImportEnabled` | Scheduled catalog import, default `false` |
| `Media:Igdb:ArtworkEnabled` | Refresh browsed game covers, default `false` |
| `Media:Igdb:ScheduleHourUtc` | Daily import hour, default `3` |
| `Media:Igdb:PageSize`, `Media:Igdb:PageBudget` | Default 100 games/page and 10 pages/run; unfinished runs resume |
| `Media:Igdb:RequestsPerSecond`, `Media:Igdb:MaxConcurrentRequests` | Shared import/artwork budget, default 3/8; maxima 4/8 |
| `Media:Igdb:TimeoutSeconds`, `Media:Igdb:LeaseSeconds` | Default 20/120 seconds |
| `Media:Igdb:IncrementalOverlapMinutes` | Replay overlap, default 10 minutes |
| `Media:Tmdb:ReadAccessToken` | TMDB API read access bearer token |
| `Media:Tmdb:Enabled` | Movie/TV artwork lookup, default `false` |
| `Media:Tmdb:TimeoutSeconds` | Default 20 seconds |
| `Media:Tmdb:RequestsPerSecond`, `Media:Tmdb:MaxConcurrentRequests` | Shared in-process budget, default 4/4 |
| `Media:Artwork:PositiveCacheDays`, `Media:Artwork:NegativeCacheDays` | Default 30/7, each bounded to 1–150 days |
| `Media:Artwork:PollSeconds`, `Media:Artwork:BatchSize` | Default 2 seconds/10 claims |
| `Media:Artwork:LeaseSeconds`, `Media:Artwork:MaxAttempts` | Default 120 seconds/5 attempts per demand cycle |
| `Media:Artwork:RetrySeconds`, `Media:Artwork:MaxRetrySeconds` | Default 60/21600 seconds; longer provider Retry-After is honored |

Environment variables use double underscores (for example `Media__Tmdb__ReadAccessToken`). Set provider flags only after credentials are configured. Restart the backend after changing options. The `Testing` and `Integration` environments do not register these hosted jobs. Import uses the existing daily scheduler; setting an hour does not trigger an immediate startup import. Incremental overlap must be at least one minute to replay timestamp boundaries safely.

For the first supervised live check, use one backend process and verify that staging and the due-artwork queue are small. A conservative temporary configuration is IGDB `PageSize=5`, `PageBudget=1`, `RequestsPerSecond=1`, `MaxConcurrentRequests=1`, `TimeoutSeconds=10`, `LeaseSeconds=120`; TMDB `RequestsPerSecond=1`, `MaxConcurrentRequests=1`, `TimeoutSeconds=10`; artwork `BatchSize=1`, `PollSeconds=5`, `MaxAttempts=1`. Keep normal cache lifetimes. These settings are recommendations, not enabled defaults. Wait for the owner to confirm credential readiness before live calls.

`PageBudget` counts game pages. Separate HTTP limits count every actual import send, including Twitch, game types, discovery, retries and empty terminal pages. Admission has separate row/batch limits. A small ascending-ID sample may contain no eligible games. Inspect sanitized stop reasons, request counts, committed cursor, admission backlog and elapsed time before expanding allowances. Small smoke checks do not establish bulk throughput; request pacing and token state are shared only within one process.

The daily IGDB schedule remains finite and resumable. Each invocation shares cumulative HTTP, admission-row and time allowances across bounded work units, with a fresh service scope and a one-second yield between units. `PageBudget` caps total game-page requests across those units. It may stop below that cap because HTTP, admission, elapsed-time or provider limits are reached. Completing the current scan ends the invocation; a later invocation starts incremental refresh, whose fixed upper watermark and overlap remain unchanged across resumed units.

### Finite Catalog Bootstrap

IMDb retains at most 8 Mi raw TSV characters per callback batch (`MaxBatchCharacters`, maximum 32 Mi), as well as the configured row limit (`BatchSize`, default 5,000, maximum 50,000). List preallocation is capped at 5,000 entries. `MaxLineCharacters` must fit within the aggregate character limit. Wide rows cause an earlier batch flush. These caps bound batch input and row overhead; total process memory still requires full-feed calibration.

The legacy `POST /api/Test/triggerImdbImport` and `POST /api/Test/triggerImdbLoad` routes return 410 ProblemDetails. Use the catalog job below; manual triggers cannot bypass its calibrated profile or complete-feed gate.

Bootstrap defaults to `Media:Bootstrap:Provider=none`. Select exactly one provider on the current application's command line. A selector or session allowance supplied only through appsettings, user secrets or environment variables is rejected. Normal configuration precedence still applies; the effective values must match explicit launch arguments. Effective raw `Media:Igdb:Enabled` and `Media:Igdb:ImportEnabled` must agree when both exist, including a legacy false value in appsettings.

Use a single supervised foreground process with automatic restart disabled. Verify its database connection points to the intended isolated target before launch. Do not reuse the running original application's port or database. Configure credentials outside source control. A deliberate new process grants a new allowance; the application cannot distinguish a human relaunch from a supervisor replaying the same arguments.

Example finite IGDB launch from the repository root, after the target and credentials have been authorized:

```powershell
dotnet run --project MediaRankerServer/MediaRankerServer.csproj --no-launch-profile -- `
  --urls=http://localhost:5257 `
  --Media:Igdb:Enabled=true --Media:Igdb:ImportEnabled=true `
  --Media:Bootstrap:Provider=igdb `
  --Media:Bootstrap:MaxHttpAttempts=100 `
  --Media:Bootstrap:MaxAdmissionRows=5000 `
  --Media:Bootstrap:MaxSeconds=900
```

This command shape is covered by configuration tests; it was not launched against live providers. The numbers are a finite example, not a certified catalog-wide throughput profile. Bootstrap work units default to 5 game pages, 20 HTTP attempts, 500 admission rows, one admission batch and 60 seconds; `Media:Bootstrap:IgdbYieldMilliseconds` defaults to 1000. Every unit retains the same cumulative session allowances. Shared request pacing includes artwork and Twitch; the import HTTP counter excludes independently initiated artwork traffic.

These are independent ceilings: with one admission batch per unit, a page of eligible rows can end the unit before its page or row ceiling. Game types are currently rediscovered in every unit that fetches pages, consuming at least one additional HTTP attempt per unit. Include that overhead, token/discovery calls and the between-unit yield in any proposed live profile; the configured page limit alone is not a throughput forecast.

Watch the session ID, stop reason, HTTP operation counts, committed staging/admission counts, durable cursor, pending incremental window, retry count and lease-busy expiry. Reserved admission rows can exceed committed rows after a failed batch. Provider cooldown stops bootstrap immediately without charging an unsent request. A busy lease sends no upstream HTTP; wait until the reported expiry before deliberately relaunching. Do not force-clear the lease. IGDB database statements default to a separate 15-second limit (`Media:Igdb:MaxStatementSeconds`, allowed 1–120); any tighter existing context limit is preserved. Admission query failures stop as admission-blocked and require diagnosis.

Use Ctrl+C to stop. IGDB resumes from its last atomically committed staging cursor; failed pages do not advance it. Existing eligible staging can drain while upstream service is unavailable. Once the durable bootstrap scan is complete, explicit bootstrap only drains admission and preserves any pending incremental window. Replay preserves a newer cover and its expiry. A blocked admission batch requires diagnosis before restart; do not repeatedly relaunch an unchanged failing prefix.

Both catalog schedules wait during bootstrap. Success resumes each enabled schedule at its next daily time, without catch-up. Any cap, failure or stop leaves both paused for that process, including release-day admission. Artwork processing remains independent. During ordinary scheduled operation, IGDB admission-blocked stops only IGDB; provider faults/caps defer its next attempt until the next day.

IMDb activation additionally requires `Media:ImdbImport:Enabled=true` and `CalibratedProfileConfirmed=true`. Keep the latter false until a separately authorized full-feed calibration has succeeded. An explicit IMDb bootstrap must supply all six session allowances on its command line: `MaxHttpAttempts`, `MaxCompressedBytesPerFeed`, `MaxTemporaryDiskBytes`, `MaxDecompressedBytesPerFeed`, `MaxWholeSessionSeconds` and `MaxRowsPerFeed`, under `Media:ImdbImport`, plus `--Media:Bootstrap:Provider=imdb`. Defaults are isolated test limits: 3 requests, 64 MiB compressed/feed, 128 MiB temporary disk, 512 MiB decompressed/feed, 900 seconds and 5 million rows/feed. They are not a viable live-feed profile merely because they are finite.

IMDb always replays ratings, basics and episodes from the beginning. Cleanup/domain loading occurs only after all three feeds validate and commit successfully. Cleanup and load statements default to 1000 output rows/groups and 15 seconds per statement; feed batches default to 5000 rows. Season groups include their complete episode input. Episode staging conflicts remain unchanged, so feed replay cannot correct an existing staged hierarchy; domain loads retain their existing upsert/relink behavior. A strict feed rejection stops the process's IMDb daily schedule; a transient fault or cap defers a scheduled invocation until the next day. A new process must replay all feeds after interruption.

The retained-feed isolated replay completed with a 30-second statement limit; the 15-second default failed in future-title cleanup (a measured statement took 15.494 seconds). The committed full-feed calibration harness uses the measured 30-second profile. This does not change application defaults: explicitly select `Media:ImdbImport:MaxStatementSeconds=30` when deliberately using that tested profile. Future-title and pilot cleanup can still examine more rows than their output cap; the statement deadline bounds their duration.

Fresh first-import end-to-end calibration was waived, so the successful retained-feed replay does not certify that scenario or enable scheduled imports. Keep live profile activation separate, inspect the change's verification evidence, and authorize any fresh download or database operation explicitly. No database reset, live download or activation is implied by this runbook.

IGDB admits released main games/remakes/remasters and supplies cover references; IMDb no longer admits video games. Images are requested by the browser only when displayed. Browsed/reviewed titles register missing or expired lookups; there is no full-catalog artwork backfill. The frontend polls pending results every two seconds for up to 30 seconds per displayed set and stops when hidden or terminal.

Keep the `/credits` navigation and attribution when using providers. It includes the TMDB logo and required disclaimer, plus an IGDB link. Verify provider terms before deploying beyond the current personal/noncommercial use: [IGDB API](https://api-docs.igdb.com/), [TMDB FAQ](https://developer.themoviedb.org/docs/faq), [TMDB API terms](https://www.themoviedb.org/api-terms-of-use).

### Disposable Local Database Transition

`20260921194619_AutomaticCoverArt` drops/recreates the review view in dependency order, clears old cover associations/rows, and creates the IGDB staging/state tables. Shared Files data and S3 objects are untouched. Existing IMDb game rows are not reconciled; use a fresh local catalog for this source switch.

Stop the backend and verify the connection points to the explicitly disposable local database before running a reset. From `MediaRankerServer`, `dotnet ef database drop` prompts for confirmation; then `dotnet ef database update` applies the complete chain and system seeds. This discards local reviews/catalog data. Enable bounded IGDB ingestion and IMDb movie/TV import only after verifying the target and credentials. Start with a small page budget and inspect the logged committed counts before increasing it.

Rollback of the local transition means disabling the new jobs, restoring the previous application revision, and recreating/reseeding its disposable database. `dotnet ef database update 20260507155216_AddImdbImportRatings` reverses the new schema/view, but does not recover discarded uploads or reconcile imported game rows. No production migration procedure or S3 bucket purge is provided.

Focused deterministic verification from the repo root:

```powershell
dotnet test MediaRankerServer.UnitTests/MediaRankerServer.UnitTests.csproj --filter "FullyQualifiedName~Modules.Media|FullyQualifiedName~Modules.Reviews|FullyQualifiedName~Modules.Files"
dotnet test MediaRankerServer.IntegrationTests/MediaRankerServer.IntegrationTests.csproj --filter "FullyQualifiedName~Modules.Media|FullyQualifiedName~Modules.Reviews|FullyQualifiedName~Modules.Files"
```

Integration tests require Docker for PostgreSQL/LocalStack and use fake providers, not real API credentials. Live smoke verification is separate: configure credentials, import a bounded game sample, browse representative games/movies/series and a season/episode, verify shared posters/status and `/credits`, and record missing-provider coverage. Do not treat fake-response tests as proof of live API availability.
