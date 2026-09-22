# Dev Commands Reference

This document captures common local commands for MediaRanker.

## Local Run

- Frontend (from `MediaRankerFrontend`):
  - `pnpm run dev`
- Backend (from `MediaRankerServer`):
  - `dotnet run`

## Local Development Test User

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

## Docker Compose

- Local Postgres is configured with `max_wal_size=4GB` in `docker-compose.yml` to support large IMDB import/load operations.

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

`PageBudget` counts game pages, not token/game-type/max-ID requests, and does not cap loading an existing staging backlog into domain tables. A small ascending-ID sample may contain no eligible games. Keep the sample bounded and inspect sanitized request counts, cursor progress, queue size, and elapsed time before expanding it. Before bulk ingestion, complete data/performance review and measure per-title database work, maintenance and staging query plans, and lease headroom at representative local scale. Small smoke checks do not establish bulk throughput; request limits are shared only within one process.

With the default daily schedule and 100 rows/page × 10 pages/run, bootstrap scans at most 1,000 games per scheduled run. A catalog of N rows needs roughly ceiling(N/1,000) daily runs, possibly one more to detect the end; incremental refresh starts after bootstrap completes. These are conservative defaults, not a fast initial catalog load. Choose larger budgets only after the measurements above, accounting for provider limits and database work.

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
