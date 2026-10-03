# Proposal

## Why

The frontend has focused Node unit tests but no browser suite proving that a user can save a review through the UI and retrieve it from the real API and database. Add repeatable local browser coverage for core journeys and controlled failure states without requiring Cognito accounts or live media providers.

## What Changes

- Add Playwright with Chromium and separate real E2E and mocked browser projects, preserving existing unit tests.
- Provide a local E2E command that provisions disposable Docker PostgreSQL, applies existing migrations, starts the real API and Next.js development server, prepares deterministic fixtures, runs tests serially, and cleans up resources it owns.
- Make create, reload, edit, rerank, reload, and delete the first complete review journey. Extend coverage to catalog search, templates, validation, cancellation, and local login/logout.
- Add focused mocked browser coverage for API failures, stale responses, empty results, and artwork state transitions.
- Document prerequisites, commands, isolation, failure diagnostics, and the boundary between local application E2E and external-service verification.

## Capabilities

### New Capabilities

None. This is test tooling and regression coverage; `.openspec.yaml` explicitly sets `skip_specs: true`.

### Modified Capabilities

None. Existing review-library, catalog-browsing, and template-management requirements remain authoritative and unchanged.

## Impact

- Frontend package manifest/lockfile, Playwright configuration, browser tests, and test harness scripts.
- Dedicated test-only Docker configuration and local development documentation; generated reports and runtime artifacts excluded from version control.
- Development prerequisites: Docker, the repository's .NET SDK, Node/pnpm, and Playwright Chromium installation.
- No application database schema changes, public API additions, relaxed authentication guards, deployment changes, or CI workflow changes.

## Non-goals

Production-build certification, live Cognito flows, live artwork/import services, multi-account security coverage, cross-browser matrices, performance testing, and comprehensive visual snapshots are outside this initial suite.
