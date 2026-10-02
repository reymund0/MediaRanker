## 0. Preconditions

- [x] 0.1 Branch `codex/tv-series-hierarchy` from the current `main`. If `design-system` has merged, follow its layer and token rules; otherwise use theme values and route `_components` folders. Leave unrelated working-tree changes untouched.
- [x] 0.2 Read `design-reference/README.md` and the five `.dc.html` screens. Use their layout, copy and states; ignore their sample data.
- [x] 0.3 Prepare the `mediarank_ui` clone for local work and screenshots. Do not migrate, reset or run the IMDb import against the owner's `mediarank` database without approval.

## 1. Schema and backfill

- [x] 1.1 Add `SeasonNumber` to `MediaCollection`, `EpisodeNumber` to `MediaEntity`, and `MediaCollectionId` (nullable `MediaId`) to `Review`, with the indexes, check constraint and filtered unique index from `design.md`. No cross-module foreign keys.
- [x] 1.2 Create one migration: columns, indexes, backfill SQL (season numbers from integer titles, episode numbers from `imdb_import_episodes`), and a recreated `review_details` view with the fields in `design.md`. Keep the view SQL as a module artifact under `Reviews/Data/Views`. Write a deterministic `Down`.
- [x] 1.3 Apply it to `mediarank_ui` and record the duration here. Verify every integer-titled season has `season_number`, numbered-season episodes have `episode_number`, and `Unknown` seasons have none (SQL counts recorded here).

## 2. IMDb load

- [x] 2.1 Season and episode load SQL: skip `season_number = -1`, write `season_number` and `episode_number` on insert and on conflict update.
- [x] 2.2 Add the bounded unknown-season cleanup step after the episode load, with review guards and per-batch logging, following the existing load provider and `MaxStatementSeconds` patterns.
- [x] 2.3 Integration tests: unknown rows not created from the feed; existing unknown episodes, seasons and emptied series deleted; a reviewed unknown-season episode keeps its episode, season and series; numbers written on reload.

## 3. Media API

- [x] 3.1 `GET /api/MediaCollection` filters (`mediaType`, `collectionType`, `parentId`), series relevance search, series and season counts, start and end years, and hiding of series with only unnumbered seasons.
- [x] 3.2 `GET /api/media` `mediaCollectionId` filter and episode-number ordering. Add nullable `episodeNumber`, `seasonNumber`, `seriesId` and `seriesTitle` to `MediaDto`.
- [x] 3.3 TV series removal: delete series, seasons and episodes in one transaction and publish `SeriesDeletedEvent`. Provide the episode and review counts the confirmation dialog needs (a count endpoint or fields on the series DTO).
- [x] 3.4 Integration tests: series list (search order, counts, hidden all-unknown series, hand-added series with no seasons), seasons in numeric order (10 after 9), unnumbered seasons excluded, episodes paged in episode order, series removal.

## 4. Reviews API

- [x] 4.1 Insert validation: exactly one target; a collection target must be a TV `Series`; the template must be TV; one series review per user. Keep update and delete unchanged.
- [x] 4.2 `ReviewDto` fields from `design.md` (`kind`, `mediaCollectionId`, series and episode fields, series years and counts), mapped from `review_details`.
- [x] 4.3 `SeriesDeletedHandler` deletes the series review and episode reviews.
- [x] 4.4 Integration tests for 4.1–4.3, with ProblemDetails assertions on rejections and a regression test that game and movie reviews are unchanged.

## 5. Frontend

- [x] 5.1 Contracts: update `src/app/reviews/contracts.ts`, media contracts and the collection DTO type. Fix every `mediaId` use for nullability.
- [x] 5.2 Catalog TV list (`CatalogTV.dc.html`): series rows, nested seasons and episodes, "Show N more episodes" (25 per batch), "Review series", "Review" and "View review", series Edit/Remove with the counting confirmation, TV "Add a title" creating a series, and the `?series={id}` deep link.
- [x] 5.3 Ranking groups: rank within `(mediaType, kind)` and use labels "#R of N TV series" / "#R of N TV episodes" everywhere a TV rank shows.
- [x] 5.4 Library TV (`LibraryTV.dc.html`): episode-aware latest highlight, Series poster grid and Episodes ranked list.
- [x] 5.5 Drawer (`ReviewDetailEpisode.dc.html`, `ReviewDetailSeries.dc.html`): context lines, cross-links, "Series review" card, "Episodes you've reviewed" list, "Browse all episodes". Edit and delete work for both kinds.
- [x] 5.6 New-review dialog (`NewReviewTV.dc.html`): series search, series view with "Review series" and the season/episode tree, reviewed marks, episode context in step 2, "Change" back to the series view. Starting from a Catalog row opens step 2 with the right target.

## 6. Verification and review

- [x] 6.1 Run `pnpm lint`, `npx tsc --noEmit` and the frontend node tests. Run the targeted backend test classes for 2.3, 3.4 and 4.4. Ask the owner before running the full backend test suites or `pnpm build`.
- [x] 6.2 Live check on `mediarank_ui` with the local test login. Record each result here:
  - Catalog TV: open a series and a season. Open a season of more than 25 episodes and use "Show more".
  - Review a series and two episodes. Check the Library TV grid, list and ranks.
  - Open both drawers and follow the cross-links.
  - Add and remove a test series.
  - Delete the fixture reviews afterwards.
- [x] 6.3 Capture 1440×900 screenshots matching each `.dc.html` screen into `.clanker/tv-series-hierarchy/`. Send them to the designing session (`claude --resume eb63cc15-fbd7-4347-96a4-cbafc113176e -p "..."`) for review. Resolve or record each note here.
- [x] 6.4 Update `docs/conventions/backend-conventions.md` (season and episode numbers, unknown-season cleanup, series review target) and `frontend-conventions.md` if new shared components were added.
- [x] 6.5 Run `openspec validate tv-series-hierarchy --strict`. Verify it passes.

## Verification log

- Preconditions: branch from local main 1c9133a; design-system a4a08b1 is not merged into this base. Five design sources read. Unrelated skill edits and pnpm-workspace.yaml preserved.
- mediarank_ui exists and was verified by current_database(): 1,552,539 media rows, 53,287 collections, 9,621,784 staged episodes. No reset or import needed. Owner mediarank untouched.


### Plan review notes

- Native data review and approved Claude plan review completed. Claude report: `.clanker/reviews/tv-series-hierarchy-2026-10-01-1/plan-4/report.json`; verdict remains incomplete because runtime/visual checks are pending. Detailed dispositions are in `.clanker/2026-10-01-orchestration-nation.md`.
- Rollback clarification (F1): remove series reviews only with target-owner authorization, run `dotnet ef database update 20261001043450_AddEssentialsTemplates` with the new migration still in the checkout, then revert/deploy old code. Down cannot recover import-cleanup deletions.
- Carry into implementation: atomic season/emptied-parent cleanup (F2), scanned-row cursor/skipped counts (F3), frozen migration SQL and drop-view ordering (F4/F5), record migration timeout with runtime (F6), preserve numbered zero on future loads but only positive-title backfill on measured existing data (F7), all-user removal counts/transaction (F8), hidden target insert rejection (F9), TV-qualified rank labels from task5.3 (F10), skip -1 before season LIMIT (F11).


### Schema verification

- Target: mediarank_ui; migration 20261002050653_TvSeriesHierarchySchema applied successfully in **46.127678 seconds** with a **600-second command timeout**. Log and timing: .clanker/tv-series-hierarchy/migration-apply.log and migration-timing.json.
- SQL counts: 39,216 numbered seasons; 0 positive-title seasons missing numbers; 0 Unknown seasons assigned a number; 1,129,847 numbered-season episodes, 0 missing episode numbers; 0 staging-number mismatches. All 137,966 Unknown-season episodes retained. All 3 pre-existing reviews remain visible; 0 series reviews existed.
- Targeted TvHierarchyMigrationTests: **2 passed, 0 failed** (10 seconds); covers empty staging, populated backfill, overflow titles, target XOR, pending EF model changes, reversible Down and rollback refusal with series reviews. TRX: .clanker/tv-series-hierarchy/test-results/tv-hierarchy-migration.trx.


### IMDb load verification

- Focused integration run: 23 passed, 0 failed, 12.09 seconds wall-clock (TRX: .clanker/tv-series-hierarchy/test-results/imdb-tv-cleanup-verified.trx). Covers unknown-feed exclusion, season zero, numeric reload, reviewed-first paging, reviewed episode/series preservation, already-empty series preservation and cancellation rollback/replay. Initial run: 22 passed, 1 fixture connection failure; corrected and rerun.
- ImdbLoadServiceTests: 6 passed, 0 failed (worker run); no live import or owner database operation. Cleanup logs committed deletion and reviewed-skip counts per batch.
- Upcoming live fixtures identified read-only on mediarank_ui: Breaking Bad series 3062; Days of Our Lives series 112, season 13592, 15,469 episodes.

- Native data review fixes verified: canonical season-title join prevents duplicate episode upserts for aliases such as 1/01; paging fixtures now have numbers. Parent rerun of ImdbLoadIntegrationTests, ImdbUnknownSeasonCleanupIntegrationTests and ImdbEpisodePagingTests: **28 passed, 0 failed**, 4 seconds test duration. Includes multi-page cleanup retaining a parent with a numbered season. TRX: .clanker/tv-series-hierarchy/test-results/imdb-tv-reviewed.trx.

### Media API verification

- TvHierarchyCatalogTests + MediaCollectionCrudTests + MediaCrudTests: **22 passed, 0 failed**, 3 seconds test duration (13.58 seconds runner wall-clock). Parent inspected source and retained TRX .clanker/tv-series-hierarchy/test-results/media-api.trx. Initial sandbox attempt failed at Docker setup; elevated isolated-container retry passed.
- Verified full series relevance order, numbered-only counts and years, hand-added visibility, season zero and numeric 9/10 order, all seasons returned, episode page boundaries/ties/nulls, hidden unknown lookups, all-user removal counts including hidden/direct media, event IDs and rollback on publisher failure. Actual Reviews handler deletion remains task4.


### Reviews API verification

- Focused integration classes TvHierarchyReviewTests, ReviewsCrudTests, TvHierarchyCatalogTests, ArtworkSurfaceParityIntegrationTests and MediaShowcaseTests: **25 passed, 0 failed**, 5 seconds test duration. ReviewServiceTests and MediaCollectionServiceTests: **22 passed, 0 failed**. Parent inspected source and TRX counters (reviews-api.trx, reviews-api-unit.trx). Covers target validation/ProblemDetails, duplicate protection, hierarchy context/artwork, update/delete, all-user cascade and unchanged movie/game behavior.
- Native Media API finding resolved: unsupported manual seasons rejected before insertion; hidden series updates rejected before mutation. Both regression tests passed in the integrated run.


- Native Reviews review identified hidden all-unknown series insert bypass. Parent added visibility validation plus hidden-series/unknown-episode rejection and hand-added-series success test. TvHierarchyReviewTests rerun: **6 passed, 0 failed**, 2 seconds (reviews-hidden-target.trx).

### Data implementation review

- Approved Claude packet reviewed: .clanker/reviews/tv-series-hierarchy-2026-10-01-2-data/implementation-4/report.json. No blocking findings. Overall verdict remains incomplete because the supplied hierarchy spec also contains excluded API/UI scenarios; static data requirements were covered. Native API/Reviews review and frontend/live acceptance remain separate evidence.
- F1: retained deliberate cleanup scope of IMDb TV seasons with null season_number. Measured clone has no non-Unknown unnumbered season titles; normal load refreshes valid zero seasons before cleanup. Positive-only migration backfill remains as specified.
- F2: review creation rejects unknown-season episodes and all-unknown series (6 focused tests passed). No claim of global concurrent-write serialization; operator-gated cleanup retains SQL review guards.
- F3: episode staging replay now updates existing rows; affected counts include updates and skipped counts can be zero. Duplicate staging keys in one batch fail consistently with existing basics upserts. Document telemetry semantics in backend guidance.


### Live checks in progress (mediarank_ui, local test login)

- Catalog reads: Breaking Bad shows seasons1–5; Season1 shows E1–E7 in order. Days of Our Lives Season1 (15,469episodes) first displays25rows, then Show25more appends to50, preserving E1/E25/E26/E50 and expanded season state. Early interaction check passed; final visual alignment still in progress.


- Reopened5.1 during live Addseries check: collection upsert requires numeric enum, frontend initially sent stringSeries and received400. No fixture series was created. Correction pending; earlier dialog closure was HMR, not success. Existing temporary reviews IDs7(series),8(E4),9(E6).

### Integrated frontend verification

- Numeric collection request corrected; Add series, edit name/year, and removal with 0 episodes/0 reviews passed. Disposable test series removed. Breaking Bad removal preview showed 62 episodes and 3 reviews; cancelled without deletion.
- Final lint and `npx tsc --noEmit --incremental false` passed after review fixes. All frontend Node tests: 26 passed, 0 failed, 387.2581 ms. Targeted backend results above remain valid; no backend changes since those runs. No full backend suite or pnpm build run.
- Local test login created series review7 (score9), episode review8 (S1E4, score8), and review9 (S1E6, score10). Library shows 1 ranked series and 2 separately ranked episodes; latest episode badge/context and independent #1/#2 labels verified.
- Both drawer cross-links work. Browse all episodes closes the drawer and opens the linked series in Catalog. Deep-linked series collapses with one click; subsequent search remains editable. Picker searches series only, disables already-reviewed targets, shows episode context in scoring, and Change returns to the series. Footer Cancel now closes the dialog (live checked).
- Final Catalog pagination recheck: Days of Our Lives Season1 has 15,469 episodes; 25 Review buttons became50 after Show25more, preserving expanded state. Capture: CatalogTV-paging.jpg.
- Native code review: six original findings fixed; final Cancel regression fixed and source rechecked. Native visual review identified episode context/type labels, now corrected. Screenshot clipping was a capture-coordinate artifact: DOM reports fixed drawer x840/y0, width600/height900 at1440x900; viewport recaptures show full-height drawer and backdrop.

### Design review dispositions and deeper browser QA

- Designing session review saved to `.clanker/tv-series-hierarchy/design-review.txt`; corrected/supplemental screenshots sent to the same session (follow-up report pending).
- High1: drawer offset was page-relative screenshot clipping, not layout. Replaced captures with viewport screenshots and supplied fixed-position DOM evidence. High2/3: full episode context and TV episode label corrected; recaptured.
- Medium4/5: added season chevrons, expanded state, year and reviewed counts, framed inset tree and Or pick an episode label; recaptured.
- Medium6: retained full TV rank labels on posters because task5.3 explicitly requires these labels everywhere TV rank appears. Low7: outlined episode Review buttons. Low8: plain numeric score styling recorded as a minor visual difference; does not affect rank/score behavior. Optional template-name text and grouped empty chips left as existing behavior outside this hierarchy change.
- Supplemental 1440x900 viewport captures: LibraryTV-episodes.jpg, ReviewDetailEpisode-related.jpg, ReviewDetailSeries-related.jpg, CatalogTV-paging.jpg. These reveal previously below-fold content.
- Owner requested deeper frontend testing with browser automation: QA engineer preparing scenario matrix, separate UX reviewer explicitly gpt-6.1-sol/medium assigned live execution. Temporary fixture reviews7/8/9 remain for that run; cleanup and final acceptance pending.
- Designing follow-up report: `.clanker/tv-series-hierarchy/design-review-followup.txt`. Accepted full-height drawers, episode labels, both related sections, framed picker, Library episode list, rank-label requirement and plain scores. Remaining medium scrollbar issue fixed by existing thin/dark theme style; series poster caption now uses run years/count. Fresh outlined-button/Show-more captures assigned to live UX reviewer. Final lint/type checks passed after these edits.

- Deep browser QA confirmed rapid double-click on picker Show more could select an episode as rows moved under the pointer (twice, E25/E26; no review saved). Reopened5.6 pending retest. Paging controls now remain mounted while fetching and disabled on error (preventing a failed-page skip); episode row selection ignores the second click of a double-click. Lint/type checks passed; browser retest pending. Evidence: qa-browser/RapidPickerFailure.jpg and report.md.

- P03 picker fix verified live twice: fresh and cached page loads each double-clicked from25 to50 contiguous episodes while staying in chooser. Unintended scoring no longer reproduced. Task5.6 rechecked; deeper review/mutation/recovery scenarios continue.
- Deeper browser QA: C01–C05 passed (search,10seriespaging,resets,deep links,multipleexpansions,numericseasons1–40). P01/P03 passed afterfix (25→50→75 ordered unique episodes; double-click fresh/cached stable).
- M01/M02/M03/R01/R05/D02series/D04 passed: disposable series add/edit/cancel,0→1review removal counts, blank-writing series review, score validation/half-even preview, blank-writing related links, review edit/cancel/save. Same-year drawer/latest caption2025–2025 corrected to2025 and verified live; lint/types passed.
- M04 passed with deliberate clone backend outage: Remove disabled during loading/error, Retry restored0/0 counts, Cancel preserved title. R08 passed: failed save retained full form/error feedback; restored backend and retry created exactly one review. L02 passed: equal-score updated review moved ahead within Series, episode ranks unchanged.
- Automatic approval review blocked temporary Pilot episode review creation on existing clone title, citing scope beyond disposable series. No action executed; parent requested explicit narrow approval. Other browser cases continue. QA fixtures A Edited and B currently retained for remaining tests/cleanup; original3reviews and retained7/8/9 remain.
- Final frontend Node run after browser fixes: **26 passed, 0 failed, 369.694 ms**. Existing module-type warnings remain; no package/config changes made.
- Designing session final report (`design-review-final.txt`) accepts the inspected desktop scope with no blocking visual findings. Dark thin picker scrollbar, outlined episode Review buttons and visible Show25more accepted. Recorded nonblocking polish: singular/plural count wording, thousands separators, and Show-more link color. Previous/Next disabled behavior is implemented and assigned final DOM verification; visual dimming is a separate cosmetic observation.
- Deep browser QA verified fresh paging recovery in both Catalog and picker: exactly25 rows retained during outage; Retry appended to50 unique contiguous rows once. Ordinary game/movie scoring and original game edit-cancel passed. Disposable QA series/review deletion and reviewed-series cascade passed; parent SQL confirmed QA A/B series absent.
- Cleanup audit found unrelated review10 on The Gang Gets Racist, owned by a different user and created during this session. Preserve it alongside original reviews1/2/4. Only retained fixture reviews7/8/9 remain ours to remove.


### Final browser QA and cleanup

- QA engineer supplied 36 frontend browser scenarios; user-requested Sol6.1 Medium reviewer executed them. Final report: `.clanker/tv-series-hierarchy/qa-browser/report.md`. **32 pass, 2 partial, 1 blocked, 1 P2 narrow-screen failure.** No unresolved required desktop failure. Execution began before approximately00:04 PDT and final assertions ended01:02 PDT on October2; exact start was not instrumented. Documented bounded duration from00:21:37 to01:02:11 is40 minutes34 seconds, including outages, repairs and captures.
- Two discovered desktop defects were fixed and retested: rapid picker paging accidentally selected an episode; equal start/end years displayed twice. Final lint/types and26Node tests passed.
- Partial/blocked coverage remains explicit: extra Pilot review creation R02 and blank-episode D02 prerequisite require the pending owner approval after automatic approval review rejected that write; artwork-pending state did not arise naturally (V03). Optional movie create/delete was omitted; ordinary movie/game scoring and original game edit-cancel passed. Existing original live series/two-episode creation checks remain passed.
- V02:1024×768 flows passed. At390×844 the desktop Catalog overflows horizontally and the existing new-review header clips its Close button; Escape/Cancel provide dismissal, and both drawers fit with reachable actions. These P2 phone-layout limitations are recorded outside the requested desktop implementation. Narrow capture scaling limits are detailed in the report; final dialog/drawer captures have verified dimensions.
- Previous/Next on a single-result page were verified natively disabled in the DOM; their bright disabled styling remains a minor visual note. Designer accepted the inspected desktop scope; all feedback was resolved or recorded.
- Parent removed retained fixture reviews9(E6),8(E4),7(series) through their review drawers after QA handoff. Final clone SQL: reviews1/2/4/10 remain; zero QA series remain; real series3062 and112 both remain. Review10 belongs to another user and was preserved. Owner mediarank remained untouched; no calibrated IMDb import ran.
- Backend/frontend convention updates document numeric hierarchy fields, hidden unknown data, bounded reviewed-data cleanup, staging replay telemetry, XOR series targets, event transaction behavior, frontend target/rank/paging conventions and deep-link state.

- Strict OpenSpec validation passed. Standard repository git diff --check also passed; CRLF conventions preserved. All28 original implementation tasks verified, with additional browser-QA partial/blocked coverage and P2 phone limitations explicitly recorded above.


## 7. Owner-approved refactor follow-up (2026-10-02)

Scope: the eight proposals in `.clanker/refactor/2026-10-02-tv-series-hierarchy.md`; preserve behavior and the documented boundaries. Earlier acceptance results remain historical evidence.

- [x] 7.1 Share removal membership between preview and deletion.
- [x] 7.2 Reuse Media visibility predicates through existing query builders.
- [x] 7.3 Move the TV picker subtree into a route-local component.
- [x] 7.4 Derive ranking groups and rank lookups once per owning component.
- [x] 7.5 Reuse the numbered-season query and mapper-provided start year for aggregates.
- [x] 7.6 Reuse poster ranking rendering while preserving caller styling and empty states.
- [x] 7.7 Share series year-range formatting with focused examples.
- [x] 7.8 Share pure episode merge and batch size with focused tests; retain local paging state.
- [x] 7.9 Run focused backend tests, frontend lint/types/Node checks, browser regression checks, integrated review and strict OpenSpec validation; record results and durations.


### Approved refactor verification

- Backend proposals1/2/5 implemented and parent compared against the pre-refactor snapshot. MediaCollectionServiceTests:12/12 passed (1second test duration). TvHierarchyCatalogTests + TvHierarchyReviewTests + MediaShowcaseTests:16/16 passed (4seconds test duration,14.66seconds TRX runner duration). Release/no-restore avoids locking the running Debug server; initial sandbox Docker startup failure resolved with elevated isolated-container execution. No owner database or schema changes. Evidence: `.clanker/refactor-implementation/backend-proposals-1-2-5.md` and integration `TestResults/backend-proposals-1-2-5.trx`.

- Frontend proposals3/4/6/7/8 implemented. Independent code review found and verified repairs for ordinary poster-count monospace styling and end-only drawer years. Local ESLint and TypeScript passed; npm shims were inaccessible, so installed workspace CLIs were used without configuration changes. Parent enumerated all8frontendNode testfiles: **30passed,0failed,374.8624ms**. Covers year missing/equal/range/end-only cases, merge order/replacement/immutability and full-group rank denominator for a subset. Native correctness review approved after repairs; live browser/external static review pending.

- Bounded refactor browser pass by Sol6.1Medium: **11scenarios,9pass,2partial,0fail**, **7m12.548s** active interval;1440x900 and1024x768. Catalog25→50→75 ordered/unique rows, collapse/reset, picker fresh/cached rapid double-click, episode context/Change/cancel, series scoring, ordinary poster font/ranks, Movie/TVempty states passed. Eight screenshots saved and viewed; parent also inspected LibraryGames and EpisodeScoring captures. PopulatedTVrank/drawer cases unavailable with TV0; retry/outage cases not repeated. No data persisted/deleted; live Debug backend was unchanged, with new backend separately compiled/tested in Release. Report: `.clanker/refactor-implementation/browser/report.md`. These are bounded frontend results, not full live-backend or whole-application acceptance.

- Final independent Claude implementation review: **clean**, all 41 packet subjects covered; observed model `claude-opus-5-5[1m]`, requested medium effort. Source fingerprint is current and eligible for parent review. Initial attempt had no defects but incomplete context reading; the one permitted coverage recheck completed it. Final report: `.clanker/reviews/tv-series-hierarchy-approved-refactors-recheck-2026-10-02/implementation-2/report.json`.
- Claude informational F1 is deferred outside these approved refactors: a legacy TV media review outside a Season receives kind `Title`, which the TV Library's existing Series/Episode lists omit. Parent confirmed the same filters and rank helper in the pre-refactor snapshot and the current view's kind mapping. No regression was introduced; no production/clone data scan or new behavior decision was made.
- Parent confirmed Catalog passes the complete media-type review query to the rank lookup. Proposal 5 intentionally reuses one query definition across three server-side aggregates, preserving the existing round trips. Browser partial cases remain as recorded, not converted to passes by static review.
- `git diff --check` and `openspec validate tv-series-hierarchy --strict` passed. All eight approved refactors and the bounded verification follow-up are complete (37/37 total tasks). No commits/pushes, packages/configuration/schema changes, full backend suites, `pnpm build`, owner database writes or IMDb imports. Summary and refactor-only patch: `.clanker/refactor-implementation/summary.md`.
