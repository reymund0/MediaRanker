## Purpose

Let users understand and customize which scores a review asks for.

## ADDED Requirements

### Requirement: Template cards
The Templates page SHALL list templates as cards showing name, media type, field count and field names as chips. Built-in templates SHALL carry a "Built-in" badge, explain that they are read-only, and offer "Duplicate".

#### Scenario: View a built-in template
- **WHEN** a user views a built-in template
- **THEN** the card offers "Duplicate" and no edit or delete actions

### Requirement: Side editor
Creating, duplicating or editing a template SHALL open an editor panel beside the list with name, optional description, media type, an ordered field list with drag reordering and remove, an inline "add a score" input, and Save, Discard and (for saved user templates) red Delete actions. The editor heading SHALL read "New template" for new and duplicated drafts and "Edit template" for saved ones. Unsaved drafts SHALL NOT appear in the template list.

#### Scenario: Start a new template
- **WHEN** a user selects "New template"
- **THEN** the editor opens titled "New template" and the list gains no row or card

#### Scenario: Duplicate a built-in template
- **WHEN** a user selects "Duplicate" on a built-in template
- **THEN** the editor opens titled "New template", prefilled with the name plus " (copy)", the same media type, description and field names

#### Scenario: Discard a draft
- **WHEN** a user discards an unsaved draft
- **THEN** the editor closes and the list is unchanged

#### Scenario: Validation
- **WHEN** a user saves without a name or with no fields
- **THEN** the editor identifies the problem and does not save


### Requirement: Built-in starter templates for every category
The system SHALL offer one built-in starter template per canonical category, each with four equally weighted score fields. Movies SHALL use Story, Performances, Visuals, Sound; TV shows Story, Characters, Pacing, Production; Books Writing, Ideas & themes, Structure, Engagement; Albums Composition, Performance, Production, Cohesion; and Concerts Performance, Setlist, Live sound, Atmosphere, in the listed order. The existing Video Games template and its field identities/order SHALL remain unchanged.

#### Scenario: Start reviewing any category
- **WHEN** a user selects a category without a custom template
- **THEN** its built-in four-field starter template is available and can be duplicated

#### Scenario: Preserve existing reviews when adding starter templates
- **WHEN** the starter template seed update is applied
- **THEN** existing game templates, score field identities, and reviews remain unchanged
