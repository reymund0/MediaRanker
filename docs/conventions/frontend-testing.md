# Frontend testing

Prefer real E2E coverage when representative data is convenient to create through the existing APIs. Docker is an expected local prerequisite, so needing a database alone is not a reason to mock a journey. Use controlled browser mocks when arranging the state through the real stack would be complex, slow, or nondeterministic.

| What needs proving | Suite | Data/setup |
| --- | --- | --- |
| A user action persists through the UI, API, and database | `tests/e2e/` | Real API fixtures in disposable Docker PostgreSQL; verify after reload |
| Search, ranking, templates, or ordinary validation with readily created records | `tests/e2e/` | Small deterministic fixtures using existing APIs |
| Failed saves, retries, stale responses, or hard-to-trigger artwork/provider states | `tests/browser/` | Explicit contract-shaped API/image mocks with controlled timing |
| Pure calculations, mapping, or request decisions | `tests/unit/` | Small direct inputs; no browser or Docker |

An edge case may justify a mocked test alongside a real successful journey. Choose based on the behavior being proved and the cost of reliable setup, rather than forcing every case into either suite. Empty states can use mocks when the assertion concerns rendering alone; use E2E when real querying or persistence is part of the assertion.

## Real stack boundaries

The E2E runner creates a fresh PostgreSQL 16 Compose project, applies existing migrations, and starts the host API and Next.js. "Live" here means the running local application and its disposable test database. It does not mean reusing the developer's database, personal catalog, production data, or live Cognito/artwork providers.

Use fixture APIs for setup and cleanup, and the UI for the action under test. Enter through the local development login. Preserve baseline rows, sweep UI-created records, and fail subsequent tests if cleanup cannot restore isolation. Do not intercept application API calls in the E2E suite.

## Organization and maintenance

- Group scenarios by app domain: `reviews/`, `media/`, and `templates/` where applicable.
- Keep domain helpers beside their scenarios. Use each suite's `shared/` for cross-domain fixtures and utilities; keep real API fixtures separate from mocks.
- Keep test infrastructure in `MediaRankerFrontend/scripts/browser-tests/`.
- Ground mocks in current DTOs, fulfill images locally, and fail unexpected application API or external browser requests.
- Wait for observable UI/network conditions instead of fixed sleeps. Check saved behavior after reload when persistence matters.
- Use the [E2E instruction file](../../MediaRankerFrontend/tests/e2e/CLAUDE.md) for the short contributor guide; its `AGENTS.md` symlink shares the same instructions.

## Commands

From `MediaRankerFrontend`:

```powershell
pnpm test:e2e
pnpm test:browser
node --test tests/unit/**/*.test.mjs
```

See [local browser test commands](dev-commands.md#local-browser-tests) for installation, required SDKs, focused runs, reports, process ownership, and cleanup. Both browser projects currently use Chromium, one worker, and no retries. Results cover the local development build; production builds, real authentication providers, live media providers, and other browsers require separate verification.
