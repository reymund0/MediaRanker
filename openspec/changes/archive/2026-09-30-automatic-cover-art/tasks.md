## 1. Provider configuration and access

- [x] 1.1 Add disabled-by-default IGDB import/artwork and TMDB artwork options, timing/budget/cache validation, and secret-setting documentation placeholders; verify option tests reject invalid budgets/retention and disabled integrations make no HTTP requests.
- [x] 1.2 Implement typed IGDB/Twitch clients with serialized token renewal and a shared request/concurrency limiter; verify fake-HTTP tests cover expiry, repeated unauthorized responses, 429/Retry-After, cancellation, shared import/artwork traffic, and secret-free diagnostics.
- [x] 1.3 Implement the TMDB IMDb lookup/image-configuration client and expected-result/asset validation; verify fake responses for movies, series, wrong result kind, absent posters, timeout, throttling, and server errors without live credentials.

## 2. Persistence and source identity

- [x] 2.1 Add the IGDB external-source value, module-owned import staging and committed import-state entities; verify model/index tests enforce one staging record per IGDB ID and one domain record per source/ID.
- [x] 2.2 Replace upload-specific MediaCover fields with canonical provider identity, resolution/freshness state, and concurrency/lease fields; verify the unique canonical key and legal ready/missing/pending states through PostgreSQL integration tests.
- [x] 2.3 Add migration(s) for the new entities and replacement cover representation, clearing old cover associations only as documented and updating dependent review views in the correct order; verify the complete migration chain on an empty test database and confirm historical migrations and shared Files schema remain unchanged.

## 3. IGDB catalog ingestion

- [x] 3.1 Implement current game-type discovery and paged IGDB metadata reads including cover references; verify fixture-based selection admits released main games/remakes/remasters and excludes DLC, expansions, bundles, ports, editions, unknown dates, and future releases without requiring ratings or covers.
- [x] 3.2 Implement bootstrap/incremental run bounds, stable ID pagination, overlap, per-run serialization, and atomic staging-page/cursor commits; verify interruption/replay, equal timestamps spanning pages, concurrent-run exclusion, and failed commits never advance progress.
- [x] 3.3 Implement idempotent staging-to-domain loading, game cover-reference attachment, and source-version/fetch-time guards; verify changed metadata updates existing IDs, stale responses cannot replace newer covers, missing covers are accepted, and no image binaries are fetched.
- [x] 3.4 Implement scheduled admission of staged future games independently of upstream update timestamps and repeat-load freshness protection; verify a game appears on release day after a zero-change sync and local staging rereads do not renew cover TTLs.
- [x] 3.5 Register the resumable scheduled IGDB job with page budgets, sanitized progress counts, and disabled Testing defaults; verify failures preserve committed catalog data and subsequent bounded runs resume unfinished work.
- [x] 3.6 Remove IMDb video-game staging/loading and its vote configuration without changing movie/TV behavior; verify updated ImdbLoadIntegrationTests and ingestion tests show no IMDb games while movies, series, seasons, and episodes still load under existing thresholds.

## 4. Demand-driven artwork resolution

- [x] 4.1 Implement canonical movie/game/series lookup identities and effective series artwork fallback in Media queries; verify episodes/seasons share one state and newly imported children see the same cover without a bulk descendant update.
- [x] 4.2 Register deduplicated artwork demand after authorized browse/search/review selection and successful review persistence; verify only returned titles are registered, concurrent reads create one work item, and registration/provider failure does not undo a valid review.
- [x] 4.3 Implement the short-interval artwork worker with atomic claims, expiring leases, conditional completion, bounded attempts, and delayed retry; verify restart recovery, no duplicated active claims, provider-specific throttling, and correct classification of no-image versus transient/auth failures.
- [x] 4.4 Implement fresh IGDB reference reuse and expired-reference refresh by ID alongside TMDB IMDb resolution; verify fresh games require no discovery call and a delayed worker result cannot overwrite a newer import result.
- [x] 4.5 Implement 30-day positive/7-day negative freshness defaults, expired-reference withholding, and database-only removal of expired TMDB fields independently of network enablement; verify dormant data never triggers provider HTTP calls and retained TMDB metadata cannot exceed the configured cap below six months.
- [x] 4.6 Implement expected-host HTTPS image URL construction, image size selection, and non-pending disabled/unsupported/failure states; verify URLs contain no credentials, reject invalid provider asset values, and do not route through S3.

## 5. Remove upload coupling and unify responses

- [x] 5.1 Remove CoverUploadId from media/collection upsert contracts, validators, and mutation logic; verify ordinary title/date edits preserve provider identity and automatic cover associations and manual media creation still succeeds without a cover.
- [x] 5.2 Remove Media cover-upload endpoints/types, cover-upload service methods, and S3 cover-cleanup job/DI registrations while retaining shared Files functionality; verify authenticated old routes return 404 with no upload side effects and independent Files tests still pass.
- [x] 5.3 Update media/collection/unreviewed/review DTO mappings and review SQL projection to use one effective provider cover/status contract; verify the same movie/game/TV cover and freshness status appear on every surface, including placeholders and removed posters.
- [x] 5.4 Add integration cases for authorization and provider-disabled behavior across demand/refresh paths; verify anonymous access is rejected, another user's review data stays inaccessible, and no client can submit arbitrary provider/image requests.

## 6. Automatic frontend experience

- [x] 6.1 Remove the media edit upload control, mutations, schema field, and frontend contract fields, and update any existing collection editing surface; verify edited titles save without file interaction and refreshed data does not clear artwork or unsaved inputs.
- [x] 6.2 Render provider URLs with appropriately sized/lazy images, descriptive alt text, and a stable image-error placeholder in the media grid, unreviewed selection, and review cards; verify delayed, missing, unsupported, and failed images in the rendered app at desktop and mobile widths.
- [x] 6.3 Add bounded pending-only React Query refresh at two-second intervals for a maximum of 30 seconds per active view; verify completion, terminal state, timeout, hidden document, and unmount stop polling without resetting filters, page, review order, or open forms.
- [x] 6.4 Add a navigable About/Credits page with an approved TMDB logo/current required disclaimer and IGDB link; verify keyboard access, readable layout, correct links, and visible attribution in the rendered app.

## 7. Integrated validation and operating guidance

- [x] 7.1 Replace upload-specific cover fixtures and complete focused unit/integration scenario coverage from all three specs using fake provider responses; verify targeted runs of the affected Media/Reviews/Files tests pass with no external credentials or provider calls, recording exact commands and any environment blockers.
- [x] 7.2 Run targeted frontend lint and type checking and inspect the integrated browse/edit/review/credits flows; verify network evidence for lazy images and bounded polling, record screenshots/findings, and resolve failures before claiming visual acceptance.
- [x] 7.3 Update relevant repository guidance and dev commands for provider setup, required attribution, job controls, cache policy, and local reset/reseed/rollback; verify instructions against implemented options, keep secrets out, and state explicitly that no production migration or S3 bucket purge is provided.
- [x] 7.4 Once credentials and the explicitly disposable local target are available, run a bounded opt-in provider smoke check and local reset/reseed validation; verify representative IGDB games and IMDb/TMDB movie/series artwork, record observed coverage/remaining gaps, and leave this task incomplete if credentials or runtime dependencies block execution.
- [x] 7.5 Review the complete working-tree implementation against this proposal/specs and repository conventions, resolve actionable findings, and verify all required evidence is recorded before marking implementation complete; do not automatically archive the change or publish a PR.

## 8. Approved development test login

- [x] 8.1 Add an opt-in local development test identity and frontend login/session/logout flow; verify fixed identity, successful loopback access, and rejection with the flag off, outside Development, or from a non-loopback connection without weakening Cognito authentication.
- [x] 8.2 Document and run the local test login, then finish the authenticated browser checks for media/edit/review/credits, responsive artwork placeholders, lazy images, and bounded polling; record any provider-dependent states still unavailable without credentials.

## Review acceptance — 2026-09-21

The owner accepted the remaining review limitations ("let's just consider it complete enough") and authorized closing task 7.5. This is an explicit acceptance waiver, not a clean or complete Claude verdict. Native reviews and recorded validation remain the completion evidence. The latest Claude report retained partial/unreviewed coverage, including four excluded test fixtures, and two plausible minor observations requiring further evidence (token-endpoint failure cooldown and recoverable import/artwork concurrency delays). These remain documented follow-up risks, not resolved findings. Representative performance measurements remain required before bulk API activity; this acceptance does not enable provider jobs or authorize bulk imports.
