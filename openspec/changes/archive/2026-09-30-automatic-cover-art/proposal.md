## Why

Imported media currently lacks automatic cover art, and uploading covers adds an unwanted user step. Using IGDB for both game identity and artwork removes unreliable matching between IMDb games and a separate image catalog, while existing IMDb movie/TV identities can resolve TMDB posters directly.

## What Changes

- Import game metadata and available cover asset references from IGDB, with resumable initial ingestion and incremental refresh. Initial catalog scope is released main games, remakes, and remasters; exclude bundles, DLC, and special editions.
- **BREAKING**: Stop ingesting/loading IMDb video games. Rebuild the disposable local catalog without IMDb-to-IGDB reconciliation or preservation of local reviews.
- Resolve movie and TV artwork through TMDB using existing IMDb IDs. Use a series poster for its seasons and episodes in the initial version.
- Fetch missing/stale artwork metadata only for titles returned by browse/review flows. IGDB imports may collect cover references with catalog metadata; image bytes load only for displayed content. Do not perform a whole-catalog artwork backfill.
- Serve provider-hosted images, cache resolution metadata with bounded lifetimes, and retry transient failures without delaying or failing browsing/reviewing.
- **BREAKING**: Remove manual cover-upload controls, cover-upload endpoints, and cover-upload fields from media/collection edit contracts. Preserve normal title/date editing and review creation.
- Present placeholders for missing/failed images, refresh visible covers after background resolution, and display provider attribution.
- Add the user-requested, opt-in localhost development test login with a stable test identity so browser verification does not depend on Cognito credentials. Production authentication remains Cognito.

## Capabilities

### New Capabilities

- `igdb-game-catalog`: IGDB identity, game selection, resumable ingestion/refresh, and retirement of IMDb game ingestion.
- `automatic-cover-resolution`: Provider-backed cover references, demand-driven resolution, TV poster sharing, retry/caching behavior, and provider configuration.
- `automatic-cover-experience`: Upload-free media/review flows, consistent cover URLs, visible refresh, placeholders, and attribution.

### Modified Capabilities

None. No main capability specifications exist yet; the new specs capture both the introduced behavior and the existing interfaces it changes.

## Impact

- Backend: Media import providers/services/jobs/entities, cover lifecycle and DTO mappings; Reviews read projections and demand registration; EF migrations and related tests.
- Frontend: media editing, collection surfaces where present, media grids, unreviewed search, review cards, bounded artwork refresh, and application credits.
- Development authentication: guarded local test scheme, login/logout/session integration, and focused negative security checks; no external account provisioning.
- Integrations: server-side TMDB credentials and Twitch application credentials for IGDB; provider request budgets, token refresh, and image CDN availability. No paid plan or new library is required by this proposal.
- Storage: replace the upload-specific cover representation with provider metadata and durable resolution state. Remove cover-specific S3 lifecycle usage while retaining shared Files functionality needed elsewhere.
- Operations: add disabled-by-default provider jobs/configuration and document a local-only reset/reseed procedure. Existing migration history stays immutable; this planning change does not reset any database.
- Non-goals: production data conversion, manual correction/upload UI, fuzzy game matching, artwork for books/albums/concerts, self-hosting provider images, screenshots/gallery UI, commercial licensing, and replacing IMDb movie/TV imports. Manually created records without provider identity retain a placeholder.
