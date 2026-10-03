# Design

## Context

See proposal.md for motivation and scope. The frontend uses Next.js 16, React 19, React Query, and Node's test runner for isolated tests under `tests/unit`; its package manifest has no browser runner. Backend integration tests already use disposable PostgreSQL 16 through Testcontainers and migration-driven setup.

The development login is gated by frontend development mode, an explicit flag, and localhost. Its identity is stored in sessionStorage and the API requires Development, an explicit flag, and direct loopback requests. The normal Docker Compose database uses a persistent `pgdata` volume and is unsuitable for automatic browser-test resets. The API registers AWS services and Development background jobs, including enabled file cleanup, so the harness must supply inert configuration and explicitly disable those jobs.

## Goals / Non-Goals

**Goals:** Repeatable browser evidence of real persistence, deterministic data, isolated resource ownership, actionable failures, and a separate fast browser suite for controlled API states.

**Non-Goals:** See proposal.md. In particular, local-login tests do not prove Cognito behavior or account isolation, and mocked response tests do not prove API contracts or persistence.

## Decisions

### Two explicit browser projects

Use `@playwright/test` as a development dependency with Chromium initially. Put real journeys in `MediaRankerFrontend/tests/e2e`, controlled-response tests in `tests/browser`, and shared helpers in a nearby support directory. Proposed public commands are `pnpm test:e2e` and `pnpm test:browser`; these are planned additions, not existing commands. Keep unit tests separate.

Real E2E must not intercept application API requests. Mocked tests must stub their API traffic explicitly and fail on unexpected application API calls; they start only the frontend and require no Docker or backend. Use accessible roles, labels, and observable states instead of CSS implementation details or fixed sleeps. Add minimal labels/test IDs only where needed for stable interactions; avoid application behavior changes.

All-mocked coverage would miss persistence defects. All-real coverage makes failure timing and artwork transitions unnecessarily expensive to control. The hybrid approach accepted in discussion keeps the distinction visible in project names and reports.

### Docker database, host application processes

Add a standalone E2E Compose file for PostgreSQL 16 with a unique run-scoped project name, loopback-bound ephemeral host port, and disposable storage. Do not merge the normal development Compose configuration or reuse `pgdata`. Use a Node harness with argument-array process spawning to coordinate Docker, migration commands, `dotnet run`, Next.js, and Playwright on Windows and other supported developer platforms.

Run API and frontend as host processes on dedicated loopback ports; fail clearly on occupied ports rather than attaching to an existing server. This preserves the direct-loopback authentication boundary. Running the API inside Docker could make requests appear non-loopback and would require an unnecessary auth change.

On Windows, invoke Next and Playwright through Node and their resolved JavaScript entry points, not `.cmd` shims. Launch .NET without a launch profile and with an explicit loopback URL. Track process trees, terminate the owned tree on Windows (and the owned process group on Unix), and verify the ports are released. Run the API with a run-scoped working directory and explicit content root so its hard-coded relative Serilog file sink cannot mix test output into developer logs. For `dotnet run`, explicitly set the MSBuild `RunWorkingDirectory` property to the absolute owned directory; the Web SDK otherwise replaces the parent process working directory with the project directory. Pass the absolute backend content root separately and verify the actual Serilog file location.

Keep the existing Next output configuration. Before starting, refuse clearly if the checkout's `.next/dev/lock` exists; never remove a developer lock or attach to that server. Serialize both browser projects against that shared output location. Document that tests reuse the generated Next cache, so the developer server must be stopped and restarted with ordinary environment settings afterward. Preserve tracked `tsconfig.json`; verify test runs do not alter it. A separate distDir is deferred because Next can rewrite generated-type includes and would require extra application configuration.

Runtime verification found a repeat-launch Turbopack `next/font/google` query-parser failure. Select the installed Next CLI's `--webpack` option only in the browser-test harness; retain the ordinary development command, output configuration, and product fonts. Webpack startup succeeded with the same assets. This suite does not certify Turbopack or remove the existing compile-time Google-font network dependency.

Also preflight shared .NET build output ownership: refuse a developer API/watch/debug process running from this checkout before EF or build, without stopping that process. Document that the developer API must be stopped during E2E. If ownership cannot be established on a platform, fail with actionable diagnostics rather than build into potentially active outputs. Verify this refusal on Windows; do not introduce new build-output paths or shared MSBuild configuration just for testing.

Preflight Docker availability and required tools. Obtain the actual database endpoint from the newly created container, pass that connection explicitly to migration and API processes, and never use a developer-provided/default database target for setup or teardown. Apply the existing EF migration chain without new migrations. Start only after database health, then use bounded HTTP readiness checks for both application processes. Verify a normal authenticated API read before running tests so a reachable but misconfigured API fails early.

Preflight `dotnet ef` as well as the SDK. Use a TCP PostgreSQL healthcheck (`pg_isready -h 127.0.0.1`) and a bounded check of the published host port before migration. Supply the same inert AWS and Development configuration to the EF child because EF constructs the application host; use the explicit migration connection argument in addition to the child connection setting.

Supply Development/local-login flags only to child processes. Explicitly set the frontend API origin and matching API CORS origin. Disable IMDb/IGDB imports, artwork providers, bootstrap execution, and Files cleanup; use inert AWS configuration sufficient for startup, with no live credentials or external calls. Do not weaken or bypass existing authentication guards. Docker and browser installation may require downloads during setup, but ordinary test execution must not depend on live identity/media services.

There is no artwork-worker enable flag: it remains registered and may perform housekeeping against the disposable database. Disable its upstream providers and use manual fixtures without external identities; do not invent an artwork Enabled option or change the environment to bypass registration. Observe non-loopback browser requests and fail with diagnostics if any occur, without intercepting application API traffic. Any compile-time external dependency discovered during verification is a reported blocker, not permission to alter product assets.

Set explicit child configuration overrides for `Media__Igdb__Enabled=false`, `Media__Igdb__ImportEnabled=false`, `Media__Igdb__ArtworkEnabled=false`, `Media__Tmdb__Enabled=false`, `Media__ImdbImport__Enabled=false`, `Media__Bootstrap__Provider=none`, and `Files__Cleanup__Enabled=false`. Override provider credentials with inert values and supply inert AWS/Cognito values, so Development user secrets cannot activate live integrations. Inspect all registered hosted services, including the Test module, when building the harness. Record only sanitized configuration/disabled-job evidence, never developer secrets.

### Deterministic fixtures and single-worker execution

Use existing authenticated APIs to create a small catalog and custom templates after migrations supply built-in templates. Give each test unique fixture identities and keep a record of resources created. Perform setup/cleanup in fixtures, in dependency order, using the existing API contracts; do not add production test/reset endpoints or depend on imported catalog data. Use fixed dates and controlled score differences; avoid tie assertions that depend on wall-clock timestamp resolution.

Run E2E with one worker and retries disabled initially. Tests own their data and must not depend on another test's execution order. Each browser context enters through the visible local-login action; this also avoids incorrectly assuming browser storage-state files preserve sessionStorage. Authenticate API setup separately using the existing local test scheme. The run database is the final cleanup boundary even when fixture cleanup fails.

Fixture API calls use `Bearer MediaRankerLocalTest`, retain returned resource and field IDs, and delete reviews before templates/media. Assert a clean expected starting state and explicitly choose a template when custom templates exist. Teardown attempts every recorded deletion; a cleanup failure poisons the run and prevents subsequent journeys until a fresh database is created. Ordinary media fixtures cover non-TV categories; TV uses collection endpoints, which mocked availability responses must also cover.

Snapshot baseline IDs before each test, and sweep newly created resources after it even if the browser action failed after saving. Discover reviews through every media-type listing and discover templates/media through their paginated APIs; preserve migration/built-in rows. This covers UI-created resources that fixture helpers never recorded. Attempt all deletions in dependency order, verify the baseline is restored, and poison the run on any failure. Demonstrate the sweep with an intentionally failed UI-create test before relying on suite order independence.

Always stop owned children and remove only the current run's Docker resources after success, failure, or handled interruption. Track ownership before cleanup and never perform a broad Docker prune or reset another database. Retain test reports/logs outside disposable storage. Document scoped cleanup for uncatchable process termination; a later run must not silently reuse orphaned data.

### Acceptance coverage

| Priority | Scenario | Required observation | Evidence |
| --- | --- | --- | --- |
| First | Review lifecycle | Create through catalog, reload and reopen with saved scores/notes; edit to move above another seeded review, reload and verify rank; cancel then confirm deletion, reload and verify absence/counts | Real E2E |
| Core | Local session | Local login opens authenticated UI, same-tab reload retains identity, logout returns to unauthenticated UI without previous review details | Real E2E; no Cognito claim |
| Core | Validation and cancellation | Unrated fields block Save, scoring all fields enables Save and previews the expected rounded average; cancelling creation creates no review and cancelling edit preserves saved values | Real E2E |
| Core | Catalog search | Known exact/prefix fixtures produce expected results; switching category keeps search and resets pagination; no-match state appears | Real E2E |
| Core | Templates | Duplicate a read-only built-in, change name and reorder fields, save, reload, and create/reopen a review whose field order matches | Real E2E |
| Core | API failure | Failed save surfaces an error, preserves entered content, and allows a subsequent successful save without duplicate visible entries | Mocked browser |
| Core | Late response | A controlled stale review read cannot replace an acknowledged edited review in the UI | Mocked browser |
| Core | Empty states | Empty library/catalog render their expected actions without inventing rows | Mocked browser |
| Core | Artwork | Pending becomes ready without reload; unavailable/failed artwork has a usable fallback | Mocked browser |

Exercise keyboard interaction and dialog focus/close behavior while traversing the core forms. Check the review dialog at one narrow viewport so Save remains reachable with long notes. These are focused usability assertions, not a comprehensive accessibility audit or screenshot-baseline project.

Use UI reloads as persistence evidence, not only toasts or React Query cache contents. Keep fixture API calls separate from the actions under test. Capture screenshots/traces on failure and retain frontend/API startup logs and a readable report. Surface setup failures separately from assertion failures and return a nonzero command exit code for either.

Search fixtures must produce the same asserted order under both permitted catalog orderings, unless the current chosen ordering is established from source. Use a midpoint score average to exercise server rounding. Cover the Library title chooser, Change query retention, unavailable reviewed titles, and sole-template selection within the validation/cancellation journey, alongside catalog-based creation.

The failed-save and stale-read checks protect existing implementation behavior rather than introduce new requirements: inspect the review mutation handlers and existing `tests/unit/reviews/review-query.test.mjs` before building the browser cases. If a new browser check exposes a product defect, keep the check and its task incomplete, report the evidence, and request separate authorization for any product behavior change. Do not skip, mark expected-fail, or weaken assertions to claim completion.

Mocked artwork tests fulfill image requests with local fixture bytes as well as stubbing the API DTOs. Fail unexpected external requests in the mocked project; a ready-cover response must not trigger a live CDN fetch. Real E2E continues to use no application API interception.

## Risks / Trade-offs

- Development mode differs from production builds → label results accurately; retain production/Cognito smoke verification as separate future work.
- A fixed user can cause state collisions → isolated database, serial execution, per-test fixtures, and no developer server reuse.
- Cold compilation and Docker startup are slow → bounded readiness checks and useful diagnostics; measure actual duration before choosing tighter budgets.
- Configuration can accidentally activate background work → explicit child-process configuration for all relevant jobs, no live credentials, and verify startup logs during initial acceptance.
- Mock payloads can drift → derive them from inspected DTOs and existing integration expectations; retain real journeys covering the same successful contracts.
- Process termination can bypass cleanup → run-scoped ownership and documented exact cleanup; never broaden deletion scope to compensate.

## Migration Plan

No product data migration or deployment is needed. Add the dependency/configuration and harness, prove the first review journey, then add the remaining bounded coverage and developer instructions. Verify two consecutive fresh runs and controlled setup/test failure cleanup. Removing the test configuration, scripts, and development dependency rolls back the tooling without affecting application data. CI wiring and browser-matrix expansion require separate work.

## Verification Status

Implemented and verified on Windows with Docker: four real E2E journeys passed twice against fresh databases, six controlled-browser checks passed, and all 30 existing frontend unit tests passed. TypeScript, focused ESLint, and strict OpenSpec validation passed. Setup failure, deliberate post-save failure, active-server refusal, and handled interruption exercised retained diagnostics and scoped teardown. Detailed run IDs and corrective review evidence are recorded in `.clanker/2026-10-02-orchestration-nation.md`. Unix process handling has static and simulated branch checks but has not been executed on a Unix host. No production, Cognito, live-provider, or additional-browser certification is claimed.
