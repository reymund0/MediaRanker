# catalog-browsing Specification

## Purpose

Make the catalog a search-first place to find titles to review and to manage catalog entries.

## Requirements

### Requirement: Search-first catalog
The Catalog SHALL show a prominent title search field and media type chips. Typing SHALL filter results after a short debounce. Results with a search term SHALL be ordered exact match, then prefix match, then shorter titles, then title. Types with no titles SHALL be shown as unavailable. If measured short-term search regresses badly, results MAY use prefix-then-title ordering, as permitted by design.md and task 2.2; record the comparison in tasks.md.

#### Scenario: Search titles
- **WHEN** a user types "elden ring" with Video games selected
- **THEN** matching titles appear with "Elden Ring" first, and the result count updates

#### Scenario: No matches
- **WHEN** a search matches nothing
- **THEN** the list shows a "no titles match" message with a way to add the title

#### Scenario: Switch type
- **WHEN** a user selects a different type chip
- **THEN** results switch to that type, keeping the search term, starting from the first page

### Requirement: Readable result rows
Each row SHALL show the cover, title, media type, release year (not a full date), the user's score and rank if reviewed, a primary action, and an overflow menu. Reviewed rows SHALL offer "View review". Unreviewed rows SHALL offer "Review". The overflow menu SHALL offer "Edit details" and a red "Remove from catalog".

#### Scenario: Reviewed title
- **WHEN** a result is a title the user reviewed
- **THEN** the row shows its overall score and rank and "View review" opens the review drawer

#### Scenario: Year-only dates
- **WHEN** a title's release date is stored as a placeholder day of a known year
- **THEN** the row shows only the year

### Requirement: Cover states
Cover thumbnails SHALL show the provider image when ready, a "finding art" state while pending, and a monogram placeholder when missing, failed, disabled or unsupported. Existing pending-cover polling SHALL be unchanged.

#### Scenario: Pending cover resolves
- **WHEN** a pending cover becomes ready while the row is visible
- **THEN** the thumbnail updates to the image without a page reload

### Requirement: Page scroll is never trapped
The Catalog and every list SHALL let the mouse wheel scroll the page when the pointer is over the list.

#### Scenario: Wheel over results
- **WHEN** a user scrolls with the pointer over the results
- **THEN** the page scrolls to the pagination footer

### Requirement: Manage titles
"Add a title" and "Edit details" SHALL open the media dialog restyled to Amethyst, keeping existing validation. "Remove from catalog" SHALL confirm with a red dialog naming the title.

#### Scenario: Add a missing title
- **WHEN** a user adds a title from the Catalog
- **THEN** the dialog opens with the current type selected and the new title appears after saving
