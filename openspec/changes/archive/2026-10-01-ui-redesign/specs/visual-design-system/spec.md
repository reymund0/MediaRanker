## Purpose

Give every MediaRanker screen one consistent visual system ("Amethyst") and app shell, matching the design canvas's UI kit.

## ADDED Requirements

### Requirement: Amethyst theme tokens
The system SHALL style all screens from centralized theme tokens: a cool violet-black ground, surface and raised layers, one violet accent, and a red danger color. It SHALL use Bricolage Grotesque for display and section headings, Geist for body text and controls, and Geist Mono for scores, ranks and counts.

#### Scenario: Screens use theme tokens
- **WHEN** any page, dialog, drawer, menu or toast renders
- **THEN** its colors, type and radii come from the theme, and no screen keeps the old stock styling (solid purple app bar, teal hover states, purple delete buttons)

#### Scenario: Violet is reserved
- **WHEN** a screen renders
- **THEN** the violet accent appears only on scores, ranks, the active filter, the selected template, links and that screen's single primary action

### Requirement: Destructive actions are red
The system SHALL render every delete or remove action and its confirmation button in the danger color, never in the primary violet.

#### Scenario: Confirm a deletion
- **WHEN** a user is asked to confirm deleting a review, title or template
- **THEN** the dialog names the item, states that the action cannot be undone, offers a neutral cancel action and a red delete action

### Requirement: Unified app shell
The system SHALL show, on every authenticated page, a header with the MediaRanker wordmark, primary navigation (Library, Catalog, Templates) with the current page marked, a catalog search entry, a "New review" primary action and an account menu. Content SHALL use one shared maximum width. Auth pages SHALL NOT show the header.

#### Scenario: Navigate between sections
- **WHEN** an authenticated user selects Library, Catalog or Templates
- **THEN** that page opens at the shared content width and its navigation item is marked current

#### Scenario: Open new review from anywhere
- **WHEN** a user selects "New review" in the header on any authenticated page
- **THEN** the new-review dialog opens at the title search step

#### Scenario: Catalog search shortcut
- **WHEN** a user presses Ctrl+K on an authenticated page
- **THEN** the Catalog opens with its search field focused

### Requirement: Account menu and attribution
The account menu SHALL show the signed-in name and offer change password (Cognito users only, per `password-management`), credits and data sources, and sign out. Credits SHALL NOT appear in primary navigation. Every authenticated page footer SHALL credit IGDB and TMDB and include the TMDB non-endorsement statement with a link to the Credits page.

#### Scenario: Reach credits
- **WHEN** a user opens the account menu or the footer link
- **THEN** the Credits page is reachable and still shows the TMDB logo, disclaimer and IGDB link

### Requirement: Toast feedback
The system SHALL show success and error feedback as toasts at the bottom right that do not cover the page's primary action, keeping the existing single-active-alert behavior and auto-hide durations.

#### Scenario: Save succeeds
- **WHEN** a review, title or template saves successfully
- **THEN** a success toast confirms what was saved
