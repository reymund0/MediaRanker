## ADDED Requirements

### Requirement: Series-first TV catalog
With TV selected, the Catalog SHALL list TV series, not episodes. A title search SHALL match series names only, with the Catalog's relevance order. Each series row SHALL show the cover, name, "TV series · N seasons", the run years (first to latest numbered season's year, or a single year when they match; the catalog has no "ongoing" flag, so mocks showing "2022–" mean "2022–2025"), the user's series score and rank if reviewed, and "Review series" or "View review". The overflow menu SHALL offer "Edit details" and "Remove from catalog". The list SHALL show 10 series per page.

#### Scenario: Search series
- **WHEN** a user types "breaking" with TV selected
- **THEN** matching series appear, and no episode titled "Breaking…" appears as a row

#### Scenario: Reviewed series
- **WHEN** a series row is a series the user reviewed
- **THEN** it shows the overall score with "#R of N series" and "View review" opens the series review drawer

#### Scenario: Series with only unnumbered seasons
- **WHEN** a series' seasons all lack a season number
- **THEN** the series does not appear in the list

### Requirement: Expand series and seasons
Selecting a series row SHALL expand it to show its numbered seasons in season-number order, each with "Season N", its episode count, its year and "R of N reviewed". Selecting a season SHALL expand it to show its episodes in episode-number order, each with "E" plus the number, title, year, the user's score and rank if reviewed, and "Review" or "View review". Seasons SHALL NOT be reviewable. Several series and seasons MAY be open at once. Expanded rows SHALL be visually nested under their parent and expose their open state to assistive technology.

#### Scenario: Open a series
- **WHEN** a user selects the Breaking Bad row
- **THEN** Seasons 1–5 appear beneath it in order, and Season 10 would follow Season 9, not Season 1

#### Scenario: Open a season
- **WHEN** a user selects Season 1 under Breaking Bad
- **THEN** episodes E1–E7 appear in episode order with their titles

#### Scenario: Review an episode from the catalog
- **WHEN** a user selects "Review" on an episode row
- **THEN** the new-review dialog opens at step 2 with that episode selected and its series, season and episode shown

### Requirement: Paged episodes
A season SHALL show its first 25 episodes when opened and a "Show N more episodes" action while more remain. That action SHALL append the next 25 under the season without collapsing or moving other open rows.

#### Scenario: Large season
- **WHEN** a user opens a season with 15,469 episodes
- **THEN** 25 episodes appear with "Show 25 more episodes", and each use appends 25 more

### Requirement: Stored season and episode numbers
The catalog SHALL store a season number on TV season collections and an episode number on TV episode media. Ordering of seasons and episodes SHALL use these numbers. Seasons without a number SHALL NOT be returned by browsing endpoints, and neither SHALL their episodes.

#### Scenario: Existing catalog after migration
- **WHEN** the migration runs on a catalog with IMDb staging data
- **THEN** every season titled with a positive integer has that season number, every episode in those seasons has its IMDb episode number, and `Unknown` seasons have none

#### Scenario: Unknown season before cleanup
- **WHEN** a series still has an `Unknown` season because no import has run since the migration
- **THEN** browsing shows only its numbered seasons
