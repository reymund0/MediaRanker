## Purpose

Let users see their reviews as best-first rankings and create, read, edit and delete reviews without cramped in-card forms.

## ADDED Requirements

### Requirement: Best-first rankings
The system SHALL return a user's reviews for a media type ordered by overall score descending, then most recently updated, then ID. The Library SHALL show them as a poster grid where each poster shows the cover, its 1-based rank and its overall score, with the first-ranked poster visually highlighted.

#### Scenario: View rankings
- **WHEN** a user opens the Library with reviews in the selected media type
- **THEN** posters appear highest score first, each labeled with its rank and whole-number overall score

#### Scenario: Equal scores
- **WHEN** two reviews in a type share an overall score
- **THEN** the more recently updated review ranks higher

### Requirement: Media type filter
The Library SHALL offer a filter chip per media type showing that type's review count. It SHALL select the first type with reviews by default and SHALL NOT render empty rows for types without reviews.

#### Scenario: Switch type
- **WHEN** a user selects a different type chip
- **THEN** the grid shows that type's rankings and the chip is marked selected

#### Scenario: No reviews in some types
- **WHEN** some types have no reviews
- **THEN** the Library shows one prompt to browse the catalog instead of an empty row per type

#### Scenario: No reviews at all
- **WHEN** a user has no reviews
- **THEN** the Library shows an empty state with a "New review" action

### Requirement: Latest review highlight
The Library SHALL feature the user's most recently updated review with its cover, title, year, rank, headline, notes excerpt, per-field scores and overall score, and SHALL link to its detail.

#### Scenario: Open latest review
- **WHEN** a user selects "Open review" on the highlight
- **THEN** the review drawer opens for that review

### Requirement: Review drawer
Selecting a poster SHALL open a side drawer showing the cover, title, year and media type, rank within its type, overall score, each field score as a 10-segment bar plus number in template field order, the headline, full notes, and reviewed/edited dates. The drawer SHALL offer Edit and Delete. Edit SHALL switch the drawer to an edit form with the same scoring control as creation, and Cancel SHALL return to detail without saving.

#### Scenario: Field order after saving
- **WHEN** a user saves an edited review and the drawer returns to detail
- **THEN** field scores appear in the template's field position order

#### Scenario: Delete from drawer
- **WHEN** a user confirms deletion in the red confirmation dialog
- **THEN** the review is deleted, the drawer closes, the grid and ranks update, and a toast confirms it

### Requirement: New-review dialog
The system SHALL create reviews in a two-step dialog. Step 1 SHALL let the user choose a media type and search unreviewed titles with results showing cover, title and year in relevance order, and SHALL mark titles the user already reviewed as unavailable. Step 2 SHALL show the chosen title with a "Change" action that returns to step 1 keeping the search query, preselect the template when the type has exactly one, and collect an optional headline, optional notes and a score per template field.

#### Scenario: Only one template
- **WHEN** the chosen type has exactly one template
- **THEN** step 2 selects it automatically and says it was picked automatically

#### Scenario: Write a long review
- **WHEN** a user writes multi-paragraph notes in step 2 or in drawer edit mode
- **THEN** the notes field spans the form width and grows with the text, the dialog body scrolls, and the overall score and Save stay visible in the pinned footer

#### Scenario: Change the title
- **WHEN** a user selects "Change" in step 2
- **THEN** step 1 shows again with the previous query and results, and the dialog stays open

#### Scenario: Start from the catalog
- **WHEN** a user selects "Review" on a catalog row
- **THEN** the dialog opens at step 2 with that title selected

#### Scenario: Dismiss the dialog
- **WHEN** a user closes or cancels the dialog
- **THEN** no review is created and the Library is unchanged

### Requirement: Whole-number scoring
Each template field SHALL be scored with a ten-button control taking integers 1–10. Scores in a new review SHALL start unrated and display "Not rated". Save SHALL stay disabled until every field is scored. The dialog SHALL preview the overall score using the same averaging and rounding as the server.

#### Scenario: Unrated start
- **WHEN** step 2 opens for a new review
- **THEN** every field shows "Not rated", no segments are filled, and Save is disabled

#### Scenario: Live overall preview
- **WHEN** a user scores fields
- **THEN** the overall preview updates to the server-rounded average of the scored fields and shows how many fields are scored

#### Scenario: Save a complete review
- **WHEN** every field is scored and the user selects Save
- **THEN** the review is created, the dialog closes, the Library shows it in rank order and a toast confirms the save
