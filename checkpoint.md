# MediaRanker checkpoint — local catalogs loaded, artwork enabled

## Closeout in progress (2026-09-26)

Bootstrap review corrections passed 81 focused unit and 13 integration tests; the native rereview found no remaining defect in that repair scope. The earlier full ordinary suites passed 297 unit and 125 integration tests. Claude Opus 5.5/medium completed with partial coverage; the user explicitly waived the remaining external coverage in favor of native final reviews. The harness now fails on failed measurements, IMDb logs affected counts and retained failure progress, batch yields are cancellable, and calibration records the measured 30-second statement limit. Application defaults and scheduled imports remain unchanged.

Next: checkpoint these corrections, integrate upstream MR-51 (`d2d2e8b`), apply the user-approved data-preserving migration compatibility correction, verify the combined branch on disposable databases, then sync/archive `bulk-catalog-bootstrap` and publish the PR. The loaded local database must not be migrated during this closeout. The following sections retain the earlier replay handoff and its historical state; they do not supersede this closeout status.

## Cleanup repaired; full retained-feed replay passed (2026-09-26)

The approved orphan-cleanup repair is implemented and verified. It materializes the configured candidate page (1,000 ordered IDs in verification) before the orphan check and advances an invocation-local cursor over examined rows, including zero-delete pages. Actual-delete counters, fresh EF contexts, the all-feed success gate and existing single-writer/episode-refresh semantics are preserved. Compatibility overloads retain their previous behavior; the importer uses the new paged path. No schema, dependency or running-app configuration change was made.

**Full retained-feed replay passed in 1335.733 seconds (22.3 minutes)** using the existing importer and complete movie/series/season/episode loader on the disposable clone. Import plus cleanup: 724.083s; domain load: 581.733s. Exactly one local read of each of the three retained gzip feeds was used for replay; zero external provider requests. The separate brief cancellation check also passed. Final staging counts and all five semantic hashes exactly matched the baseline: IMDb identities/metadata/hierarchy, collections, cover freshness, IGDB identities and staging. No temporary files remain.

**Use a 30-second SQL allowance for the measured full-size replay profile.** The preceding 15-second attempt completed feeds but timed out in future-title cleanup. A bounded actual DELETE measurement took 15.494s under 30 seconds and was rolled back. This was a valid unit needing headroom, separate from the orphan query defect. The successful 30-second replay retained the original absolute deadline `2026-09-27T00:22:36.178045Z`; no session allowance was renewed. The code's isolated-test default and normal app configuration were not changed; this evidence does not automatically activate or certify scheduled imports.

Full replay guard: 1341.14s elapsed, peak runner RSS 277,569,536 bytes, sampled PostgreSQL directory peak 11,067,445,248 bytes. Bounds held: 2 GiB runner RSS, 58 GiB test database directory, 2 GiB temporary files, 30-second SQL, 5,000-row import batches, 1,000-row cleanup/load units, original overall deadline plus 30-second stop reserve. PostgreSQL container remains capped at 4 GiB / 2 CPUs. This is one retained-feed replay measurement, not a future throughput guarantee or a fresh end-to-end calibration.

Cleanup-only evidence: 269,302 pending orphans removed across 9,892 pages, restoring staging 9,891,086 to 9,621,784 with all other counts/hashes unchanged. Cleanup plus final snapshot took 69.012s; provider-call median 5.2306ms, p95 7.7381ms, max 969.205ms under 15-second SQL. After the separate future-title timeout, baseline preparation under 30 seconds removed 3,688 future titles, one pilot and 284,302 orphans; all hashes again matched. The first unpooled cleanup helper failed without deleting rows, with thousands of TIME_WAIT sockets and no PostgreSQL timeout/error. Its exact exception cause was not retained; a separate pooled helper succeeded within its unchanged deadline. Failed attempts are preserved.

Current focused checks: **29/29 passed** (18 unit / 11 PostgreSQL integration). Native review approved the five-file cleanup delta. Strict OpenSpec validation and diff checks passed. Broader external-review task **6.2 remains open (21/22)**; no new paid Claude review was run. Fresh end-to-end calibration remains explicitly waived, not passed. No commit, PR or archive was made.

Disposable target: container `1231836572dddda8e5fea3a5dc179f90b6cf45c3b3302cc27bf324b51bd8e26f`, database `imdb_replay_20260926`, cluster `7689978574401822764`; last loopback port **64827** (recheck after restart). The test container is stopped and retained after verification. Main app/database were untouched by this continuation; scheduled imports remain off and demand-driven artwork remains enabled. The normal app binary was not replaced by isolated builds. Source edits are uncommitted.

Evidence: `.clanker/imdb-cleanup-20260926/` (plans, 29-test TRX, five-file source fingerprints, first helper failure), `imdb-cleanup-20260926-2/` and `-3/` (successful cleanup measurements), `.clanker/imdb-replay-20260926-2/` (15-second future-cleanup failure) and `-3/` (successful full replay). All ignored one-use helpers must not be automatically rerun. Append-only orchestration log remains `.clanker/2026-09-21-orchestration-nation.md`, run `bootstrap-1`.

## Earlier replay failure (2026-09-26)

The user waived a fresh end-to-end IMDb calibration, prioritized replay and limited cancellation/recovery effort. This is a waiver, not an end-to-end pass; external-review task 6.2 remains open.

Six current-source focused checks passed (3 unit / 3 PostgreSQL integration): response-read and between-batch cancellation, episode replay advancement, stable identity/relinking, staging conflicts, and idempotent movie updates. Initial stale binaries discovered 0/1 tests; only `*-current.trx` proves all six. A brief retained-file cancellation passed in 7.805 seconds including snapshot verification: one 64 KiB ratings read, cancellation observed, no temporary files, unchanged database semantics.

**Full-size replay failed during orphan-episode cleanup at the 15-second SQL statement limit**, after 891.873 seconds (guard 897.09), before domain loading. It used only retained files and a disposable copy of the completed catalog. Cleanup removed 3,688 future titles, one TV pilot and 15,000 orphan episodes before stopping. The saved plan scans/joins large staging inputs and sorts before LIMIT 1000. This is now a demonstrated replay blocker; do not label replay passed or silently raise the timeout.

All five post-failure semantic hashes match: IMDb media identities/metadata/hierarchy, collections, cover freshness, IGDB media and IGDB staging. Test staging basics/ratings remain 12,345,173 / 1,713,250; episode staging is 9,891,086 because cleanup is incomplete. No temporary files remain. This proves failure containment, not complete replay or in-flight SQL cancellation/crash recovery. Peak RSS 278,282,240 bytes; sampled PG directory peak 10,440,818,688 bytes.

Evidence: `.clanker/imdb-replay-20260926/` includes current TRX, retained SHA-256 verification, clone/guard, `output/result.json`, `post-failure.json`, `cleanup-plan.json`, `handoff.json`. A pre-database Windows trailing-separator rejection is preserved under `preflight-rejected/`; its fix retained the original deadline. One-use scripts must not auto-restart.

Disposable container `1231836572dddda8e5fea3a5dc179f90b6cf45c3b3302cc27bf324b51bd8e26f` (`mediaranker-imdb-replay-20260926`, port 58291, database `imdb_replay_20260926`, cluster `7689978574401822764`) is stopped and retained for diagnosis. It was copied read-only from the main catalog in 151.83 seconds. The main app and DB remain available with artwork enabled; API/frontend returned HTTP 200. No application source/schema/configuration edits, paid review, commit or PR occurred. Next work: fix bounded orphan cleanup and select a new finite replay attempt.

## IGDB complete; artwork enabled (2026-09-26)

The normal local catalog now contains **228,582 eligible IGDB games** from **376,382 staged records**. Exact eligibility verification found all 228,582 present, zero wrong metadata and zero missing/stale cover state; pilot identities and IMDb counts are unchanged. Bootstrap is complete and the lease is released. **218,371 game covers are Ready; 10,211 are negatively cached Missing.** No importer is running.

IGDB artwork and TMDB are now enabled in project Development user secrets, each at **1 request/second and one concurrent request**. IMDb import and both IGDB import aliases stay false; bootstrap stays `none`. Artwork is demand-driven, with 30-day positive and 7-day negative caching; image bytes come from provider CDNs. Existing credentials were preserved and never copied into evidence. API session `9506` is the current normal local host; frontend remains on port 3000.

Live movie/series requests transitioned Pending to Ready for Star Wars: Episode IV - A New Hope and Game of Thrones. The game returned by the Half-Life search was Doom but its Half-Life 2 (IGDB 387186), already Ready from import. Repeated reads preserved all three cache timestamps, expiry, outcome and attempt counts. Frontend returned HTTP 200. This was an API/database smoke check, not rendered-browser inspection or a new IGDB artwork network lookup.

The initial 100-request pilot succeeded. The catalog supervisor then hit a Windows sharing error replacing its progress file after 1,544.06 seconds and stopped its owned API. Committed state was preserved (359,400 staging rows / 219,729 games / cursor 395693). A separately reviewed recovery used 69 actual HTTP attempts and 8,853 admission rows in 82.067 seconds, reporting Completed/catalogReady. Its finite 400-HTTP / 30,000-row / 600-second allowance retained the original catalog absolute deadline. Guard fixes were limited to bounded status-file retry, deadline rechecks and confirmed child exit. Exact HTTP totals for the interrupted catalog segment are unavailable; do not invent a whole-run request total.

Evidence: `.clanker/igdb-local-20260926/` contains preserved pilot/catalog/recovery records, `catalog-verified.json`, `artwork-first-lookup.json`, `artwork-verified.json` and `app-verified.json`. One-use administrative scripts must not be rerun against the completed database. Full-change external review and IMDb full-feed calibration limitations below remain open; this local activation does not close them.

Date: 2026-09-26 (America/Los_Angeles)
Workspace: `G:/Development/dotnet/MediaRanker` (normal local checkout)
Branch: `codex/bulk-catalog-bootstrap`
Last commit: `77e72e5d897de858aea992a2c315219cc263ca5c`
Baseline: `346ccc6c8e446b69cd235de3ef9e5a87904e63c6`

## Current local state

The user authorized normal local startup/import and transfer of validated test data into the main local Docker database, then resumed work after a pause. **The remaining episode load is complete.** No importer, restore or calibration is running.

- Frontend: `http://localhost:3000/auth/login` — select **Use local test user**.
- API: `http://localhost:5157`, current rebuilt source, Development, loopback test identity enabled.
- PostgreSQL: container `a97d65e0637a` / `mediaranker-postgres-1`, PostgreSQL 16, database `mediarank`, loopback port 5432; cluster identifier `7688082602112733222` was checked by both Docker and the administrative connection.
- Scheduled imports remain disabled and bootstrap is `none`. IGDB artwork and TMDB lookups are enabled on demand at 1 request/second / one concurrent request each.
- Last app tool sessions: API `9506`, frontend `31301`. Recheck listeners/processes before reusing these handles.
- No database reset, additional feed download, remote deployment, paid Claude review, commit, push, PR or archive occurred in this continuation. Working-tree changes remain uncommitted.

## Verified catalog

| Table/category | Rows |
| --- | ---: |
| IGDB staging | 376,382 |
| Eligible video games | **228,582** |
| IMDb basics staging | 12,345,173 |
| IMDb ratings staging | 1,713,250 |
| IMDb episode staging | 9,621,784 |
| Movie-category titles (including shorts/video/TV movies) | 56,144 |
| Loaded TV episodes | **1,267,813** |
| Series collections | 12,875 |
| Season collections | 40,412 |

Exact eligibility verification found 1,267,813 eligible episodes, 1,267,813 matching media rows and **zero wrong season/type links**. All 70,000 preexisting episode ID/external-ID pairs were preserved. The other catalog/staging counts were unchanged. Authenticated browse endpoints returned HTTP 200 with movie/episode/collection totals 56,144 / 1,267,813 / 53,287; the frontend login page returned HTTP 200. No rendered-browser inspection is claimed.

## Episode repair and evidence

`MediaRankerServer/Modules/Media/Data/ImdbLoadSqlProvider.cs` now scans a materialized, ordered, bounded basics candidate page before eligibility joins. Cursor/HasMore follow scanned candidates even when no episode is admitted. It resolves the uniquely qualified TV season once. This avoids duplicate upsert inputs when same-named seasons of different media types exist. EF result aliases use the repository's snake_case mapping.

`MediaRankerServer.IntegrationTests/Modules/Media/ImdbEpisodePagingTests.cs` covers empty admission pages, sparse later eligibility, terminal/partial pages, replay/relinking, preserved identity and same-named seasons across media types. Final focused IMDb verification passed **34/34 active tests**, explicitly excluding the inactive full-feed calibration test. An earlier 35/35 run included that opt-in test returning early; it does not establish calibration. The original draft's failing results are preserved. Candidate, root server and administrative helper builds passed with zero warnings/errors. Native correctness review approved the two-file repair. Strict OpenSpec validation and `git diff --check` passed.

Three full-size read-only SELECT-plan samples: baseline 10,842 / 9,363 / 7,093 ms for 1,000 admitted rows; candidate 139 / 90 / 51 ms for 1,000 scanned candidates yielding 157 / 147 / 87 admitted rows. Different yields and excluded upsert/WAL work prevent a direct numerical end-to-end speedup claim.

The deliberately launched episode-only helper invoked the existing `ImdbLoadService.LoadEpisodeMediaAsync`, with no host/jobs/HTTP clients/importer/migrations. It completed **9,624 batches in 481.178 seconds**, affecting 1,267,813 episodes (**1,197,813 net new**). Maximum batch: 1.0184093 seconds / 1,000 affected rows. Peak process RSS: 196,751,360 bytes. Sampled PostgreSQL directory peak: 9,759,211,520 bytes. All bounds held: 30 minutes plus 30 seconds external rollback reserve, 15-second SQL, 5-second lock timeout, 256 MiB SQL temp, 2 GiB process RSS, 58 GiB volume stop with 2 GiB reserve. The final exact eligibility query took 14.224 seconds within its 15-second SQL bound. This is one local continuation measurement, not a future performance guarantee.

Evidence: `.clanker/episode-resume-20260926/` contains the plans, final TRX, `continuation-{before,after,result,guard,progress}.json`, `catalog-verified.json`, `app-verified.json` and `continuation.log`. The helper/guard are intentionally single-attempt administrative tools; **do not rerun them**. The continuation required an exact pre-load count of 70,000 episodes, which no longer matches the completed database.

## Remaining work

- OpenSpec `bulk-catalog-bootstrap`: **21/22 tasks complete**. Task **6.2** remains open because the full-change external review coverage is incomplete; narrow native approvals do not replace or waive it. No new paid review is authorized or queued. Future authorized Claude calls use generic `opus` (Opus 5.5), fixed **medium**.
- A fresh end-to-end run is waived by the user. Full retained-feed replay and brief cancellation passed with the measured 30-second SQL profile. Keep scheduled imports disabled; see the current outcome above.
- The orphan cleanup blocker is repaired and verified; the disposable clone is stopped and retained. The source changes are uncommitted and the running normal app binary is unchanged.
- The local import request is fulfilled. Do not wipe, restore, download or replay the normal database to resume code/review work. Commit/PR/archive require their own request.

## Preserved earlier evidence

Under `.clanker/local-imdb-import/`:

- `before-transfer.dump`: 44,844-byte backup of the previous local DB; SHA256 `6025ea3c9ee3b67df4e83da0438deb2fc3ea1290ae9c1bbeb0ca1e7b6e9b973e`.
- `validated-staging-partial-catalog.dump`: 729,878,800 bytes; SHA256 `88852d96cec6bac2370ebca0083020a2f0b52b94ceb8fde733dac17203cab73f`. Historical partial snapshot, not the completed current catalog.
- `capture.json`, `restore.json`, `transfer-verified.json`: original transfer provenance and historical 70,000-episode state, preserved unchanged.

Exactly three real IMDb GETs and validated feed/staging/cleanup evidence are under `.clanker/imdb-calibration-20260923-1/`. The brace-escaping repair in `ImdbImportSqlProvider` passed focused regressions and native review. Historical full backend results (294 unit / 112 integration) predate the new episode repair and are not relabeled current full-suite runs.

Append-only orchestration log: `.clanker/2026-09-21-orchestration-nation.md`, run `bootstrap-1`. Parent owns shared log/spec/checkpoint. Check review write reservations before edits. Continue using retained routing snapshot ordinal 2 under the old `C:/Users/Raymo/.codex/worktrees/0666/MediaRanker/.clanker/routing-snapshots/` path. No worker should start new work without a bounded assignment.
