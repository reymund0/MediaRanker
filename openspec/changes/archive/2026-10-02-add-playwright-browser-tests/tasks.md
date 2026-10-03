# Tasks

## 1. Browser runner and isolated local stack

- [x] 1.1 Add a compatible `@playwright/test` development dependency, Chromium configuration, separate E2E/mocked projects, report exclusions, and proposed package commands; verify dependency installation and Playwright configuration/test discovery without altering existing unit tests.
- [x] 1.2 Add standalone test-only PostgreSQL Compose configuration and a Node lifecycle harness using run-scoped resource ownership, ephemeral loopback database binding, explicit migration connection, bounded TCP readiness, and no existing-server reuse; verify a fresh database migrates, occupied ports fail clearly, and an active developer API/watch/debug process from this checkout is refused before touching shared build output or the normal Compose database.
- [x] 1.3 Start host API and Next.js with the documented local-login flags, explicit bindings/no launch profile, matching API/CORS origins, inert AWS settings for migration and API children, and disabled imports/artwork providers/bootstrap/Files cleanup; allow the existing artwork housekeeping worker on the disposable database, verify an authenticated real API read succeeds, and fail on observed non-loopback browser requests.
- [x] 1.4 Implement owned-process-tree/container cleanup and retained run-scoped logs on success, setup failure, test failure, and handled interruption; exercise each failure path, verify owned ports are released, only current-run resources are removed, and failures return nonzero exit codes. Refuse an existing Next dev lock without touching it and verify tracked tsconfig stays unchanged.
- [x] 1.5 Document Docker, SDK, Node/pnpm and browser installation prerequisites, command behavior, development-mode limitation, diagnostics, and exact orphan cleanup in `docs/conventions/dev-commands.md`; verify the documented startup command reaches the isolated stack on Windows.

## 2. First real review journey

- [x] 2.1 Add API fixture helpers for deterministic media/templates/reviews, baseline-ID discovery and dependency-ordered cleanup including UI-created resources, plus visible local-login setup per browser context; deliberately fail after a UI save and verify the sweep restores baseline state, teardown attempts all deletions, and failed cleanup blocks later tests rather than leaking state.
- [x] 2.2 Implement the create/reload/edit/rerank/reload/delete journey with another fixture review as a ranking reference, including cancelled deletion; verify persisted scores/notes, ordering, counts, and final absence after reload against the real API without request interception.
- [x] 2.3 Add trace/screenshot failure capture and clear startup-vs-assertion diagnostics; deliberately fail the journey once to verify usable artifacts survive teardown, then restore and pass it.
- [x] 2.4 Document how to run the focused journey and inspect its failure report; verify the literal documented command selects and runs that test.

## 3. Core regression journeys

- [x] 3.1 Cover local login, same-tab reload, logout, incomplete scoring, midpoint rounding, and cancelled creation/edit; exercise the Library title chooser, Change query retention, reviewed-title exclusion, and sole-template selection. Verify persisted data remains unchanged after cancellation and authenticated details clear after logout.
- [x] 3.2 Cover deterministic catalog search, no matches, retained query on category change, and pagination reset using enough fixtures to reach a later page; verify displayed results and page state against existing catalog requirements.
- [x] 3.3 Cover built-in template read-only actions, duplication, reordered fields, reload, and use in a saved review; verify saved template and review field order matches the chosen order.
- [x] 3.4 Add focused keyboard/dialog focus checks and a narrow-viewport long-notes case to the relevant journeys; verify controls remain operable and Save is reachable without fixed sleeps.
- [x] 3.5 Update the browser-coverage documentation with scenarios and exclusions; verify each listed scenario maps to a named executable test and that multi-account/Cognito coverage is not implied.

## 4. Controlled frontend states

- [x] 4.1 Configure frontend-only browser execution with complete explicit application API stubs and unexpected-call failures; verify `pnpm test:browser` runs with the API stopped and without invoking Docker.
- [x] 4.2 Add failed-save/retry and delayed-stale-response scenarios using payloads grounded in actual DTOs; verify error feedback, preserved input, successful retry, and protection of the edited UI from a late read.
- [x] 4.3 Add empty library/catalog and pending-to-ready/missing/failed artwork cases; fulfill image requests with local fixture bytes, fail unexpected external requests, and verify expected actions and cover updates/fallbacks without contacting live artwork services or CDNs.
- [x] 4.4 Document how mocked coverage differs from E2E and how to maintain fixtures; verify documented commands run only their intended projects and fixture examples match current response contracts.

## 5. Integrated acceptance

- [x] 5.1 Run the completed E2E suite twice from separate fresh run databases with one worker and no retries; record exact commands, duration, outcomes, and cleanup evidence to establish order-independent repeatability.
- [x] 5.2 Run the mocked suite and existing affected frontend unit tests; perform the focused lint/type checks appropriate to changed files and record results, resolving regressions before marking complete.
- [x] 5.3 Review the final harness and test changes against the design acceptance matrix and repository review guidance; verify authentication guards, production configuration, developer database/volumes, and unrelated working changes remain untouched, and document any unrun acceptance checks.
