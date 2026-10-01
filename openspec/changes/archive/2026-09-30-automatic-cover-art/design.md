## Context

See [proposal.md](proposal.md) for motivation and scope. The repository uses feature modules, EF Core/PostgreSQL migrations, typed HTTP clients, module-owned SQL providers, and hosted services. Existing IMDb ingestion stages data before loading domain records.

Relevant current behavior:

- `Modules/Media/Data/Entities/MediaEntity.cs` stores `ExternalSource`/`ExternalId` with a partial unique index. `MediaCollection` represents series and seasons. Season external IDs repeat the series IMDb ID.
- `ImdbLoadSqlProvider` currently imports games together with movies and synthesizes July 1 release dates from IMDb years. IGDB imports will use provider release timestamps, not those synthetic dates.
- `MediaCover` requires upload/S3 fields. `MediaCoverService` uses the two-phase Files upload lifecycle and publishes file-deletion events during cleanup.
- Media and collection edits currently mutate covers using `CoverUploadId`. Removing only the frontend control would clear covers or fail collection updates.
- Media/unreviewed DTO mappers and `Reviews/Data/Views/ReviewDetailView.sql` assume an S3 file key. The frontend already consumes cover URL strings and renders ordinary image elements.
- `Shared/Jobs/BaseJob` schedules daily work; it fits catalog refresh but not prompt artwork resolution. Test fixtures disable external jobs and use PostgreSQL/LocalStack for relevant integrations.

The user considers the local database disposable. This removes the need to reconcile existing IMDb games or preserve local reviews, but does not authorize a production reset or rewriting historical migrations.

## Goals / Non-Goals

**Goals:** Keep ownership in Media, use existing infrastructure, make imports restartable, share TV artwork by identity, and preserve responsive authenticated reads. Capture durable state in PostgreSQL without a new queue package or provider SDK.

**Non-Goals:** A general metadata aggregation framework, arbitrary image URLs, copied provider binaries in S3, provider matching UI, per-platform game identities, or production data conversion. The capability specs define the remaining product boundaries.

## Decisions

### 1. Separate catalog identity from artwork identity

Add `Igdb` to `MediaExternalSource`; use the IGDB game ID as the external ID. Keep movies/TV as IMDb records. Implement IGDB catalog providers beside the existing IMDb providers and register typed clients through `MediaModule`.

Keep the existing `CoverId` relationships, but repurpose `MediaCover` as a provider-reference and resolution-state entity. Replace file-upload columns with:

- Provider (`Tmdb` or `Igdb`), lookup kind (`MovieImdb`, `SeriesImdb`, `IgdbGame`), and lookup ID, uniquely indexed together.
- Optional resolved provider item ID and image asset path/ID; no full arbitrary remote URL.
- Outcome (`Pending`, `Ready`, `Missing`, `Failed`), checked/expiry timestamps, request timestamp, next-attempt time, attempt count, sanitized failure code, and claim token/lease expiry.
- Existing created/updated timestamps; a concurrency guard for completion and import updates.

Source lookup IDs originate in the catalog. TMDB item IDs and image paths are separately expiring provider data. One record holds both result and work state, avoiding a separate queue table for this bounded workflow. A unique canonical key deduplicates concurrent demand.

Alternative: retain upload and remote variants indefinitely. Rejected because the user wants automatic-only covers and has no local data preservation requirement. A new sibling artwork entity would duplicate the existing cover relationships. The latest migration will clear old cover associations/rows before replacing their representation; it must not delete unrelated files or data.

### 2. Import IGDB through resumable staging

Use a module-owned `IgdbImport` staging entity keyed by IGDB ID for the fields needed for selection and loading: name, first-release timestamp, game type, edition-parent identity, cover reference, provider updated timestamp, and fetched timestamp. Store upcoming supported games here so they can become eligible as time passes. Resolve supported game-type identifiers against IGDB's current game-type data; do not reuse deprecated category-enum assumptions.

Use a small `IgdbImportState` record for bootstrap/incremental phase, fixed run bounds, committed last ID, and last completed update watermark. Request pages in ascending ID order. Bootstrap scans a bounded ID range; subsequent runs filter by an overlapping updated-time window and paginate by ID within that window. Holding the time window fixed for the run avoids offset drift and does not split equal timestamps across an advancing timestamp cursor. Capture the bootstrap start watermark so updates concurrent with bootstrap are replayed afterward.

Commit each staging page and its cursor atomically. Serialize catalog runs with a database lock/lease on their state record, without holding a long transaction across HTTP. Domain loading is idempotent by `(ExternalSource, ExternalId)` and resumes from committed staging even after a separate domain-load failure. Never advance the completed run watermark before all fetched pages are committed. A small overlap on incremental windows intentionally replays data; older provider versions must not overwrite newer staged/domain data.

After each successful scheduled ingestion, load newly eligible staged games and refresh existing eligible records. Re-evaluate staged future releases on every successful scheduled run, even when upstream reports no changes. Missing covers and missing votes do not exclude eligible games. Preserve one game per IGDB ID rather than one per platform. Do not infer deletion from an incomplete page or absence from an incremental window; upstream deletion/reclassification reconciliation is outside this first version.

Import available cover references into `MediaCover` with their actual provider-fetch-time freshness and attach them to the game. A repeat local staging load must not extend that freshness or overwrite a newer on-demand lookup. Explicit absence becomes a negative result. This stores metadata only; no image request is made. A game lookup by its IGDB ID handles later on-demand refresh when the reference expires. Newer import results and worker completions use conditional updates so a delayed response cannot replace newer data.

Use `BaseJob` for the disabled-by-default scheduled importer, with a bounded page budget per run and persisted resume state. Remove IMDb game acceptance, loading branches, vote settings, and associated test expectations without changing movie/TV thresholds. Do not create a parallel IMDb game catalog.

Alternative: search IGDB during every user search. Rejected because it would couple browsing to upstream latency and replace the current local catalog model. Partner-only dumps are not required.

### 3. Resolve only demanded artwork, with shared TV ancestry

After existing authenticated queries select their returned records, a Media service bulk-registers missing or stale canonical cover work. Use the returned page/list, never the unpaged search query. Reviews calls this service after its existing user filtering and after successful review persistence; artwork registration failures are logged and must not undo the review.

Movies use their IMDb ID with TMDB find; accept only the expected movie result. TV seasons/episodes walk existing parent relationships to the series IMDb identity and resolve only a show poster. Keep the cover relationship on the series and calculate the effective cover through ancestry in read projections. This avoids updating every episode when a poster changes and automatically covers newly imported descendants. Missing ancestry yields unsupported/placeholder state. Games use IGDB identity directly; fresh imported references need no API call.

Add an independently configurable short-interval `BackgroundService` to process requested, due rows. Claim a bounded batch transactionally with a lease and token, release the database transaction before HTTP, and complete only with the matching claim/version. Expired claims are recoverable after restart. Start with one worker per application instance; database claims protect against overlapping instances. Do not use fire-and-forget request tasks or hold a database lock across HTTP calls.

A demand timestamp activates work; expired dormant results alone never activate network requests. Bound retries for each demand cycle (default five attempts). After exhaustion, keep a delayed failed result that a subsequent user visit can reactivate when due. Distinguish not-found/no-poster from timeout/429/5xx. Re-authenticate once for an expired IGDB token; persistent authentication failures pause that provider until a configured cooldown/reconfiguration instead of hot-looping.

Alternative: fetch covers inline with each request. Rejected because outages would slow/fail core user actions. Full-catalog TMDB backfill is explicitly out of scope.

### 4. Provider references, cache maintenance, and budgets

Use trusted HTTPS CDN bases with provider asset IDs/paths and documented image-size variants. Validate asset format and construct URLs on the server. Never expose API credentials or use the application as an arbitrary image proxy.

Proposed configurable defaults: successful references expire after 30 days, negative results after 7 days, transient retries start at one minute with exponential delay capped at six hours, and claims expire after two minutes. Validate TMDB cache configuration against a hard maximum shorter than six calendar months. A lightweight database-only maintenance pass clears expired TMDB-derived IDs/paths even for dormant records or disabled providers. Reads also reject expired references immediately. Maintenance must run independently of whether network resolution is enabled; its timing margin must prevent retention past the provider cap. Independently sourced IMDb lookup IDs remain available for a later fresh lookup.

Keep provider flags separate for catalog import and artwork resolution. Fresh known images can still render while a network worker is disabled. Missing credentials or a disabled provider produce a non-pending response state and do not accumulate runnable work. Use a shared per-provider limiter for imports and cover refresh, conservative configurable request budgets, cancellation/timeouts, and Retry-After handling. The IGDB default stays below four requests/second and eight concurrent calls. Coordinate the aggregate budget if more than one process uses the same credentials; the initial deployment assumption is one active application process.

Keep Twitch application secrets and TMDB credentials in existing secret/environment configuration. Redact authentication request URLs/headers from logs. Cache the Twitch token until shortly before its expiry and serialize renewal. Do not add a dependency when existing `HttpClient`, options, EF, and hosted-service patterns suffice.

### 5. Consistent API and frontend behavior

Retain `coverImageUrl` and `mediaCoverImageUrl`. Add a small cover status to the relevant DTOs (`ready`, `pending`, `missing`, `failed`, `disabled`, `unsupported`) so the client can distinguish work worth briefly polling from a terminal placeholder. Use one Media-owned URL/status resolver across media, collection, unreviewed, and review mapping. Update the review SQL view and its migration to project the effective provider reference and status through series ancestry, replacing the file-key-only assumption.

Remove the media edit modal's upload field, mutations, and validation. Remove `CoverUploadId` from media/collection upsert contracts and their services/validators. Remove the two Media upload endpoints and cover-specific upload/cleanup interfaces and registrations; authenticated calls to their old routes must return 404 without side effects. Keep ordinary metadata edits independent of cover relationships. Preserve the generic Files module and its tests; do not delete shared upload utilities merely because Media no longer uses them.

Use existing React Query facilities to refetch the active list while any displayed result is pending, initially every two seconds for at most 30 seconds. Stop on unmount, hidden/inactive view, no pending records, or exhausted budget. A later normal navigation/read can retry due work. Background refresh must not reinitialize edit forms, reset filters/pages, or change review ordering. Use lazy image loading, suitable small image variants, descriptive alt text, and a stable on-error placeholder that resets only when the URL changes. No new collection UI is required if no such frontend surface exists.

Add an About/Credits route linked from existing navigation, with the approved TMDB logo and exact current disclaimer and an IGDB link. Provider attribution is part of delivery, not a later task.

### 6. Verification and evidence

Use the existing xUnit/FluentAssertions/Moq conventions and deterministic fake HTTP handlers; tests must not require real provider credentials. Use PostgreSQL integration fixtures for import cursor atomicity, unique work keys, leases, shared-TV projections, and fresh migration behavior. Retain movie/TV import regression cases while replacing IMDb-game expectations with IGDB cases.

Frontend evidence includes targeted lint/type checking plus rendered checks of media editing, unreviewed search, review cards, credits, delayed resolution, hidden-tab polling, and image-load failures. No browser-test framework dependency is assumed. A small opt-in live smoke test after credentials are configured verifies provider schema and representative covers, not exhaustive catalog coverage. No tests or live imports are run during this proposal workflow.

## Risks / Trade-offs

### Approved local test login addition

During implementation the user explicitly selected a development-only local test login to enable repeatable UI verification. Provide one stable local test identity, with separate explicit opt-in backend and frontend settings. The backend registers/accepts this identity only in Development and only for direct localhost requests; production continues to use Cognito even if the development flag is accidentally set. The frontend exposes a clearly labeled local test login only in development on a loopback hostname, keeps activation in tab session storage, and clears it on logout. Use the existing authenticated API and user-scoped data flows. Do not provision an external account, embed a real password, silently auto-sign-in all users, or accept arbitrary client user IDs. Test flag-off, production, and non-loopback rejection as well as successful local sign-in and logout. Document startup settings and use this mode for the previously blocked browser acceptance checks.

- Provider outages or absent images -> placeholders and delayed retries; cover availability is not guaranteed.
- Changed provider schemas or game-type IDs -> verify current fields against official docs and sample responses before implementation; avoid deprecated fields.
- Interrupted or concurrent imports -> page transactions, persisted bounds, replay-safe upserts, and monotonic source-version checks.
- Expired reference data -> separate cache maintenance from network enablement and withhold expired URLs on reads.
- Remote images expose browser network requests to the provider -> use documented CDNs; self-hosting is outside scope.
- API budget shared by catalog and art -> shared throttling; bound catalog batches so user-demand resolution can progress.
- Local reset destroys reviews/catalog state -> acceptable only for the explicitly disposable local environment; never implement automatic startup reset or a general production purge.
- Existing tests expect upload/S3 cover data -> replace cover-specific fixtures; preserve independent Files module behavior.

## Migration Plan

1. Implement behind disabled provider flags; add new migration(s) without editing old migrations. Update/drop and recreate dependent SQL views in the proper order when replacing cover columns.
2. In the explicitly disposable local environment, stop app jobs, verify the database target, then reset and apply the full migration chain and seeds. No game-ID reconciliation or preservation migration is needed. This proposal does not perform that reset.
3. Configure credentials outside source control, enable IGDB ingestion, and import a bounded sample before continuing bootstrap. Run IMDb movie/TV imports with games excluded.
4. Enable artwork resolution and verify the specified UI flows and provider attribution. Expand import budgets only after the sample and deterministic tests pass.
5. Rollback for this local-only transition means disable new jobs, restore the prior application revision, and recreate/reseed the disposable database for that revision. It does not reconstruct discarded reviews, uploaded covers, or objects. Do not purge any S3 bucket as part of this change.

## References

Provider documentation checked during exploration/proposal; recheck terms when enabling a deployment:

- [IGDB API: game fields, paging, authentication, images, and limits](https://api-docs.igdb.com/)
- [TMDB IMDb lookup](https://developer.themoviedb.org/reference/find-by-id)
- [TMDB image URL construction](https://developer.themoviedb.org/docs/image-basics)
- [TMDB attribution and noncommercial API use](https://developer.themoviedb.org/docs/faq)
- [TMDB API cache restrictions](https://www.themoviedb.org/api-terms-of-use)
