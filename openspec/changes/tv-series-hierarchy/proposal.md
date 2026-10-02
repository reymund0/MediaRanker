## Why

The catalog stores TV as series → seasons → episodes (`media_collections` for series and seasons, `media` rows for episodes), but nothing in the app shows that shape. The TV tab in Catalog lists 1.27 million episodes one by one with no series or season context. Episodes are the only reviewable TV item, so a user can't review *Breaking Bad*, only its episodes. Seasons are titled `"1"`, `"2"`, `"Unknown"` as text and episodes have no stored episode number, so neither can be ordered correctly.

IMDb also contributes 137,966 episodes in 1,196 "Unknown" seasons, with no season or episode number, mostly from daily and talk shows. They don't fit the series → season → episode display, and the owner wants them removed rather than shown.

## What Changes

- **TV Catalog lists series.** The TV tab searches series by name. A series row expands to its seasons and a season expands to its episodes, which load in pages.
- **Series reviews.** A user can review a whole series as well as individual episodes. Series and episodes are ranked separately.
- **Context everywhere a TV review appears.** The Library, the review drawer and the new-review dialog show the series, season and episode for an episode review, and link series and episode reviews to each other.
- **New-review TV picker.** Step 1 searches series, then offers "Review series" or a season → episode tree.
- **Stored season and episode numbers.** New columns, backfilled from existing data and written by the IMDb load from now on.
- **Unknown-season data removed by the import.** The IMDb load skips episodes without a season number and cleans up the existing unknown seasons, their episodes and series left empty, without deleting anything that has a review.
- **TV "Add a title" creates a series.** Seasons and episodes are not added by hand in this change.

## Capabilities

### New Capabilities

- `tv-catalog-hierarchy`: Series → season → episode browsing, ordering and paging, and the stored numbers that drive them.

### Modified Capabilities

- `catalog-browsing`: The TV tab searches and lists series. Row content and title management differ for series.
- `review-library`: Series reviews, separate series and episode rankings, TV context in the Library, drawer and new-review dialog.
- `bulk-catalog-bootstrap`: The IMDb load stores season and episode numbers and removes unknown-season data.

## Impact

- **Database:** one migration adds `media_collections.season_number`, `media.episode_number`, `reviews.media_collection_id`, makes `reviews.media_id` nullable, adds a check and indexes, backfills the numbers and recreates the `review_details` view. There are no TV reviews today (5 reviews in total, all games and movies), so the review change carries no data risk.
- **Backend:** Media module (collection and media queries, DTOs, IMDb load SQL, series removal event), Reviews module (insert validation, DTO, view, a handler for series removal).
- **Frontend:** Catalog TV rows, Library TV section, review drawer, new-review dialog and review contracts.
- **Data removal:** the next IMDb load deletes about 1,196 seasons, 137,966 episodes and 10 series. No review is deleted by the cleanup.
- **No new packages.**
