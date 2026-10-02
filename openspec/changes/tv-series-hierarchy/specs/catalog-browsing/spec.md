## MODIFIED Requirements

### Requirement: Search-first catalog
The Catalog SHALL show a prominent title search field and media type chips. Typing SHALL filter results after a short debounce. Results with a search term SHALL be ordered exact match, then prefix match, then shorter titles, then title. Types with no titles SHALL be shown as unavailable. If measured short-term search regresses badly, results MAY use prefix-then-title ordering, as permitted by design.md and task 2.2; record the comparison in tasks.md. With TV selected, the search SHALL match series names and the results SHALL be series, as defined by `tv-catalog-hierarchy`.

#### Scenario: Search titles
- **WHEN** a user types "elden ring" with Video games selected
- **THEN** matching titles appear with "Elden Ring" first, and the result count updates

#### Scenario: No matches
- **WHEN** a search matches nothing
- **THEN** the list shows a "no titles match" message with a way to add the title

#### Scenario: Switch type
- **WHEN** a user selects a different type chip
- **THEN** results switch to that type, keeping the search term, starting from the first page

#### Scenario: Search TV
- **WHEN** a user types "the wire" with TV selected
- **THEN** the result count reads in series and "The Wire" is a series row, not a list of episodes

#### Scenario: Open a series from a link
- **WHEN** a user follows a link to `/media?mediaType=TvShow&series={id}`
- **THEN** the Catalog opens on TV with that series' name searched and its row expanded

### Requirement: Manage titles
"Add a title" and "Edit details" SHALL open the media dialog restyled to Amethyst, keeping existing validation. "Remove from catalog" SHALL confirm with a red dialog naming the title. With TV selected, "Add a title" SHALL create a TV series, and "Edit details" on a series SHALL edit its name and start year. Removing a series SHALL remove its seasons, their episodes, and every review of the series or its episodes, and its confirmation SHALL state how many episodes and reviews will be removed.

#### Scenario: Add a missing title
- **WHEN** a user adds a title from the Catalog
- **THEN** the dialog opens with the current type selected and the new title appears after saving

#### Scenario: Add a series
- **WHEN** a user adds a title with TV selected
- **THEN** a series is created and appears as a series row with no seasons, and it can be reviewed as a series

#### Scenario: Remove a reviewed series
- **WHEN** a user confirms removing a series with a series review and two episode reviews
- **THEN** the series, its seasons and episodes, and all three reviews are removed, and the Library no longer lists them
