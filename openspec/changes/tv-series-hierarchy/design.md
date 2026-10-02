## Handoff

| Item | Value |
| --- | --- |
| Visual reference | `openspec/changes/tv-series-hierarchy/design-reference/` (Claude Design sources; see its README) |
| Owner's canvas | Claude Design canvas "MediaRanker Redesign", row "TV: series, seasons & episodes" (private to the owner) |
| Designing Claude Code session | `eb63cc15-fbd7-4347-96a4-cbafc113176e` |
| Resume for review | From `G:\Development\dotnet\MediaRanker`: `claude --resume eb63cc15-fbd7-4347-96a4-cbafc113176e -p "<question or review request>"` |

Screens, one file each in `design-reference/`:

| File | Screen |
| --- | --- |
| `CatalogTV.dc.html` | Catalog, TV tab: series rows expanding to seasons and episodes |
| `LibraryTV.dc.html` | Library, TV tab: latest episode review, series posters, episode list |
| `ReviewDetailEpisode.dc.html` | Review drawer for an episode |
| `ReviewDetailSeries.dc.html` | Review drawer for a series |
| `NewReviewTV.dc.html` | New-review dialog: series search, series/episode picker, step 2 |

Sample data in the mocks is illustrative except the series, season and episode counts and Breaking Bad's season 1–2 episode titles. Mocks load episodes 10 at a time; the real page size is 25 (below).

## Context

Measured on the local catalog (2026-10-01):

| Item | Count |
| --- | --- |
| TV series | 12,875 |
| TV seasons | 40,412, of which 1,196 are titled `Unknown` |
| TV episodes (`media`, `media_type = 'TvShow'`) | 1,267,813, of which 137,966 are in `Unknown` seasons |
| Series whose only season is `Unknown` | 10 |
| Numbered-season episodes without an episode number in staging | 0 |
| Seasons not titled with a positive integer, other than `Unknown` | 0 |
| Largest season | 15,469 episodes (median 12) |
| TV reviews | 0 (5 reviews in total) |
| TV media not from IMDb | 0 |

Today:

- `media_collections` holds `Series` and `Season` rows. Season `title` is the season number as text, or `Unknown` for IMDb's `season_number = -1`. `MediaCollectionService` already enforces "a season's parent is a TV series".
- `media.media_collection_id` points an episode at its season. There is no episode number column. `imdb_import_episodes` (staging) has `season_number` and `episode_number` per `tconst`.
- `reviews.media_id` is required and unique per user. `review_details` already resolves an IMDb episode's cover through its series.
- The Catalog TV tab calls `GET /api/media?mediaType=TvShow` and lists episodes.
- The IMDb load runs non-series → series → seasons → episodes (`ImdbLoadService.LoadAllAsync`), batched and bounded by `MaxStatementSeconds`.

## Goals / Non-Goals

**Goals:**
- Browse TV as series → season → episode, in number order, with large seasons paged.
- Review a series or an episode; rank each kind separately.
- Show series/season/episode context wherever a TV review appears.
- Remove unknown-season data through the import, never deleting reviewed titles.

**Non-Goals:**
- Season reviews.
- Adding seasons or episodes by hand. "Add a title" on TV creates a series only; seasons and episodes come from IMDb. A later change can add manual seasons.
- Searching by episode title.
- Hierarchies for other media types (game or movie collections).
- Separate series and episode templates. Both use the user's TV templates.
- Phone layouts or an accessibility pass beyond the existing standard.

## Decisions

### Store season and episode numbers as columns

Add `media_collections.season_number int NULL` and `media.episode_number int NULL`, with indexes `(parent_media_collection_id, season_number)` and `(media_collection_id, episode_number)`. Keep season `title` as it is; the UI renders "Season N" from the number.

The migration backfills:

- `season_number = title::int` for TV `Season` rows whose title is a positive integer. `Unknown` seasons stay `NULL`.
- `episode_number` from `imdb_import_episodes` joined on `tconst = media.external_id` where `external_source = 'Imdb'` and `episode_number >= 0`. If staging is empty (fresh databases, integration tests), the next IMDb load fills it.

The season and episode load SQL writes both columns from now on, including on `ON CONFLICT DO UPDATE`.

Alternative considered: parse numbers at read time and sort by release date. Rejected: release dates are year-only placeholders (`make_date(year, 7, 1)`), so episode order would be wrong, and text season titles sort 1, 10, 2.

### Unknown seasons: hidden on read, removed by the import

Read endpoints never return seasons with `season_number IS NULL` or their episodes, so the UI is correct immediately after the migration, before any import runs.

The IMDb load:

1. Skips `season_number = -1` when creating seasons and episodes.
2. Adds a cleanup step after the episode load. In bounded batches it deletes:
   - IMDb episode `media` rows in `Unknown` seasons that have no review,
   - `Unknown` seasons with no remaining episodes,
   - IMDb series with no remaining seasons, that had at least one season before this step and have no series review.

   It logs counts per batch. It never deletes a row that has a review; it logs those it skips.

The cleanup reads `reviews` from Media-module SQL with `NOT EXISTS`. That is a cross-module read, accepted here for the same reason `review_details` reads `media`: it is a data guard inside a bulk SQL step, not domain logic. Do not add a foreign key.

The cleanup only runs when the IMDb load runs, which is gated by the calibrated import profile (`docs/conventions/dev-commands.md`). Until then the rows are hidden, not deleted. Don't run the import against the owner's `mediarank` database without approval.

### Series reviews live in `reviews` with a nullable target

`reviews.media_id` becomes nullable and `reviews.media_collection_id bigint NULL` is added, with:

- check `ck_reviews_target`: exactly one of `media_id` and `media_collection_id` is not null,
- unique `(user_id, media_collection_id)` where `media_collection_id IS NOT NULL` (the existing `(user_id, media_id)` unique index already allows many nulls),
- index on `media_collection_id`, and no foreign key (cross-module).

`ReviewInsertRequest` gets `MediaCollectionId`. Exactly one of `MediaId` and `MediaCollectionId` is required. A collection target must be a `Series` with `media_type = 'TvShow'`, and the template's media type must be `TvShow`. Update keeps its current shape (it updates by review ID).

`review_details` is recreated (new migration; existing migrations are immutable). It left-joins `media`, the episode's season and series, and the reviewed series. It exposes:

- `review_kind`: `Title`, `Episode` (a TV `media` row in a season) or `Series`,
- `media_id`, `media_collection_id`,
- `series_id`, `series_title`, `season_number`, `episode_number`,
- display title and media type for both targets,
- the cover id, with the series cover used for episodes and series.

`ReviewDto` (server and `src/app/reviews/contracts.ts`) gains `kind`, `mediaCollectionId`, `seriesId`, `seriesTitle`, `seasonNumber`, `episodeNumber`, plus `seriesStartYear`, `seriesEndYear`, `seasonCount` and `episodeCount` for series reviews. `mediaId` becomes nullable.

Alternatives considered:
- A `series_reviews` table. Rejected: it duplicates fields, templates, validation and ranking.
- A `media` row per series. Rejected: it creates two catalog identities for one series.

### Rankings are per kind for TV

The client ranks within `(mediaType, kind)`. For TV, "Series" and "Episodes" are separate lists, labeled "#1 of 3 TV series" and "#1 of 3 TV episodes". `sortReviewsByRank` keeps its ordering rule; callers group first. The Library TV chip count is series reviews plus episode reviews.

### Removing a series removes its whole tree

"Remove from catalog" on a series deletes the series, its seasons and their episodes. A new `SeriesDeletedEvent { SeriesId, EpisodeMediaIds }` is published, and a Reviews handler deletes the series review and all episode reviews, like `MediaDeletedHandler`. The confirmation dialog states the number of episodes and reviews that will be removed. Series removal is user-initiated and rare; it runs in one transaction.

"Edit details" on a series edits its title and start year through the existing collection upsert.

### API: extend the existing endpoints

- `GET /api/MediaCollection` gains optional `mediaType`, `collectionType` and `parentId` filters.
  - **TV series** (`collectionType=Series&mediaType=TvShow`): title search with the Catalog's relevance order (exact, prefix, shorter, title). Each row includes `seasonCount`, `episodeCount`, `startYear` and `endYear` (latest numbered season's year). It hides series whose seasons are all unnumbered, and keeps series with no seasons (hand-added ones).
  - **Seasons** (`parentId={seriesId}&collectionType=Season`): numbered seasons only, ordered by `season_number`, each with `seasonNumber` and `episodeCount`. Not paged (the largest series has well under 100 seasons).
- `GET /api/media` gains an optional `mediaCollectionId` filter and `episodeNumber` sort, ordered by `episode_number`, then title, then ID.
- `MediaDto` gains nullable `episodeNumber`, `seasonNumber`, `seriesId` and `seriesTitle`.
- `POST /api/MediaCollection` is used by TV "Add a title" (creates a `Series`).
- `DELETE /api/MediaCollection/{id}` on a TV series performs the tree removal above.

Add counts with grouped subqueries over the indexed parent columns. Don't load child rows to count them.

### Page sizes

- Series: 10 per page, the same as the Catalog today.
- Episodes: 25 per batch, with "Show N more episodes" appending the next batch under the season (Catalog and new-review picker).
- Seasons: all at once.

### Frontend structure

- Catalog: when the type is TV, `src/app/media/page.tsx` renders a TV list component in `src/app/media/_components/` instead of the media rows. Expansion state is local. Seasons are fetched when a series opens, and episodes when a season opens (TanStack Query, keyed by parent ID).
- Review experience: `openNewReview` accepts either a media item or a TV series. Step 1 for TV goes series search → series view (Review series, season/episode tree) → step 2. The step 2 title card shows a context line ("Breaking Bad · Season 1, Episode 4") for episodes. "Change" returns to the series view.
- Library and drawer render TV context from the new `ReviewDto` fields. The series drawer lists the user's episode reviews for that series from the already-loaded reviews, and "Browse all episodes" opens `/media?mediaType=TvShow&series={id}`, which searches and expands that series.
- Follow the five-layer rules in `docs/conventions/frontend-conventions.md` if `design-system` has landed. Otherwise keep new composites in route `_components` folders and use theme values, not literals.

## Risks / Trade-offs

- **Backfill runtime** (1.27M `media` rows joined to 9.6M staging rows) → test the migration on the `mediarank_ui` clone first and record its duration in `tasks.md`. Apply it to the owner's `mediarank` database only with approval.
- **Cleanup deletes data** → batched, reviewed rows excluded, counts logged, and covered by integration tests including a reviewed episode in an `Unknown` season.
- **Series removal can touch thousands of episodes** → one transaction, a confirmation dialog that states the counts, and an integration test for review removal.
- **Nullable `media_id`** affects code that assumes a media target → `tsc`, a search for `mediaId` uses, and review integration tests for both targets.
- **Cross-module SQL read in the cleanup** → documented above; no FK added.

## Migration Plan

1. Migration: columns, indexes, review target check and unique index, backfill, recreated `review_details`. `Down` restores the previous view, drops the new columns and indexes, and makes `media_id` required again. `Down` fails if series reviews exist; that is intended.
2. Deploy backend and frontend together.
3. The next approved IMDb load removes unknown-season data.

Rollback: revert the PR and run `dotnet ef database update <previous migration>` after deleting series reviews.

## Open Questions

None. Decided by the owner on 2026-10-01: series and episodes are reviewable (not seasons), rankings are separate per kind, search covers series names only, number columns are added, unknown-season data is removed by the import, and series and episode reviews share TV templates.
