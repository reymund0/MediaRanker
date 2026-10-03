# E2E test decisions

Prefer a real browser journey through the API and Docker-backed PostgreSQL when realistic data is straightforward to create with the existing fixture APIs. Use this for persistence, reloads, ranking, search, templates, and ordinary validation flows.

"Real data" means deterministic fixtures in the runner's disposable database. Use `pnpm test:e2e`; let the harness create its own Docker instance. Do not attach to an existing developer or production database, depend on imported personal data, or enable live external providers for routine tests.

Use `tests/browser/` mocks when a state is difficult or unreliable to arrange through the real stack: failed requests, precisely delayed responses, provider outages, or artwork transitions. Keep an ordinary successful journey real when practical; add mocks for the controlled edge case. Mocked coverage does not establish backend persistence.

- Put scenarios under their owning domain (`reviews`, `media`, `templates`). Keep domain helpers beside them and cross-domain setup in `shared/`.
- Import `test`, `expect`, and the local-login helper from `shared/fixtures.ts`. Preserve fixture cleanup and failure poisoning; exercise the user action through the UI and verify persistence after reload.
- Do not intercept application API requests in E2E. Keep API stubs in the separate browser suite and align them with actual contracts.
- Prefer observable conditions over fixed sleeps. Retain meaningful assertions when setup is inconvenient; choose the appropriate suite instead of weakening the test.

See [frontend testing](../../../docs/conventions/frontend-testing.md) for the decision table and [commands](../../../docs/conventions/dev-commands.md#local-browser-tests) for prerequisites and diagnostics.

`AGENTS.md` is a relative symlink to this file. Edit `CLAUDE.md` as the single source.
