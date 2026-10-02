## ADDED Requirements

### Requirement: Series reviews
The system SHALL let a user review a TV series as a whole, in addition to TV episodes and other titles. A review SHALL target exactly one title or one TV series. A user SHALL have at most one review per series. A series review SHALL use a TV template and the same fields, scoring and validation as any other review. Seasons SHALL NOT be reviewable.

#### Scenario: Review a series
- **WHEN** a user saves a complete review for the series Breaking Bad
- **THEN** the review is stored against the series, and Breaking Bad shows as reviewed in the Catalog, the Library and the new-review picker

#### Scenario: Second series review
- **WHEN** a user submits a review for a series they already reviewed
- **THEN** the request is rejected with a ProblemDetails response and no review is created

#### Scenario: Invalid series target
- **WHEN** a review is submitted for a season, for a non-TV collection, with both a title and a series, or with neither
- **THEN** the request is rejected with a ProblemDetails response

## MODIFIED Requirements

### Requirement: Best-first rankings
The system SHALL return a user's reviews for a media type ordered by overall score descending, then most recently updated, then ID. The Library SHALL show them as a poster grid where each poster shows the cover, its 1-based rank and its overall score, with the first-ranked poster visually highlighted. For TV, series reviews and episode reviews SHALL be ranked separately: series in a "Series" poster grid, and episodes in an "Episodes" ranked list where each row shows the rank, the series cover, the episode title, the series name with its season and episode code, the year and the overall score.

#### Scenario: View rankings
- **WHEN** a user opens the Library with reviews in the selected media type
- **THEN** posters appear highest score first, each labeled with its rank and whole-number overall score

#### Scenario: Equal scores
- **WHEN** two reviews in a type share an overall score
- **THEN** the more recently updated review ranks higher

#### Scenario: TV rankings
- **WHEN** a user with 3 series reviews and 3 episode reviews opens the Library on TV
- **THEN** a "Series · 3 ranked" grid and an "Episodes · 3 ranked" list appear, each ranked from #1, and the TV chip count is 6

### Requirement: Latest review highlight
The Library SHALL feature the user's most recently updated review with its cover, title, year, rank, headline, notes excerpt, per-field scores and overall score, and SHALL link to its detail. For an episode review it SHALL also show the series name with the season and episode, use the series cover with an episode badge, and state the rank among episodes.

#### Scenario: Open latest review
- **WHEN** a user selects "Open review" on the highlight
- **THEN** the review drawer opens for that review

#### Scenario: Latest review is an episode
- **WHEN** the most recently updated review is for Breaking Bad season 1, episode 6
- **THEN** the highlight shows "Breaking Bad · Season 1, Episode 6" above the episode title and "#R of N episodes"

### Requirement: Review drawer
Selecting a poster SHALL open a side drawer showing the cover, title, year and media type, rank within its type, overall score, each field score as a 10-segment bar plus number in template field order, the headline, full notes, and reviewed/edited dates. The drawer SHALL offer Edit and Delete. Edit SHALL switch the drawer to an edit form with the same scoring control as creation, and Cancel SHALL return to detail without saving. For an episode review the drawer SHALL show the series name, linked to the series review when one exists, with the season and episode, rank among episodes, and a "Series review" card when the user reviewed the series. For a series review the drawer SHALL show the run years with season and episode counts, rank among series, the user's episode reviews for that series as rows linking to their reviews, and "Browse all episodes" linking to the series in the Catalog.

#### Scenario: Field order after saving
- **WHEN** a user saves an edited review and the drawer returns to detail
- **THEN** field scores appear in the template's field position order

#### Scenario: Delete from drawer
- **WHEN** a user confirms deletion in the red confirmation dialog
- **THEN** the review is deleted, the drawer closes, the grid and ranks update, and a toast confirms it

#### Scenario: Move from episode to series review
- **WHEN** a user opens an episode review whose series they also reviewed and selects the "Series review" card
- **THEN** the drawer shows the series review

#### Scenario: Series drawer lists episode reviews
- **WHEN** a user opens a series review and has reviewed 3 of its episodes
- **THEN** "Episodes you've reviewed · 3" lists them by score with their season and episode codes

### Requirement: New-review dialog
The system SHALL create reviews in a two-step dialog. Step 1 SHALL let the user choose a media type and search unreviewed titles with results showing cover, title and year in relevance order, and SHALL mark titles the user already reviewed as unavailable. Step 2 SHALL show the chosen title with a "Change" action that returns to step 1 keeping the search query, preselect the template when the type has exactly one, and collect an optional headline, optional notes and a score per template field. With TV selected, step 1 SHALL search series by name, and choosing a series SHALL show a series view with "Review series" and the series' seasons, which expand to paged episodes. Series and episodes the user already reviewed SHALL be marked and not selectable. For an episode, step 2 SHALL show the series name, season and episode above the episode title, and "Change" SHALL return to the series view.

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

#### Scenario: Pick an episode
- **WHEN** a user searches "breaking", opens Breaking Bad, expands Season 1 and picks "Cancer Man"
- **THEN** step 2 shows "Breaking Bad · Season 1, Episode 4" above "Cancer Man", and "Change" returns to Breaking Bad's seasons

#### Scenario: Series already reviewed
- **WHEN** a user opens a series they already reviewed in step 1
- **THEN** "Review series" is replaced by "Series reviewed" with the score, and its episodes stay selectable
