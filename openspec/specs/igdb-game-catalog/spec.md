# Igdb Game Catalog Specification

## Purpose

Provide a repeatable video-game catalog sourced from IGDB, with stable game identity and associated cover references, independently of IMDb movie and television imports.

## Requirements

### Requirement: IGDB owns imported game identity
The system SHALL identify imported video games by the combination of IGDB source and IGDB game ID, retain that identity on refresh, and import available cover references with the game metadata. It MUST NOT match IGDB games to IMDb titles by name or date.

#### Scenario: Import a game with artwork
- **WHEN** an eligible IGDB game with a cover reference is imported
- **THEN** one local game is created with the IGDB identity and associated cover reference, without downloading image bytes

#### Scenario: Refresh an existing game
- **WHEN** the same IGDB game is returned again with an updated title or cover
- **THEN** its existing local record is updated without creating a duplicate or changing its local identity

#### Scenario: Missing artwork does not exclude a game
- **WHEN** an eligible game has no cover reference
- **THEN** the game remains available for browsing and reviewing with a placeholder

### Requirement: Initial game catalog has explicit selection rules
The system SHALL admit released main games, remakes, and remasters with a nonempty title and known first release date on or before the current UTC date. It SHALL exclude unreleased or undated games, DLC, expansions, bundles, ports, and edition variants from new catalog admissions. It SHALL NOT require an IMDb vote threshold, platform-specific duplicate records, or available artwork.

#### Scenario: Admit supported game types
- **WHEN** released main-game, remake, and remaster records are processed
- **THEN** each eligible IGDB identity is admitted as one game regardless of its platform count or rating count

#### Scenario: Exclude unsupported entries
- **WHEN** unreleased, undated, DLC, bundle, port, expansion, or edition-variant records are processed
- **THEN** they are not newly admitted to the browsable game catalog

#### Scenario: Previously future release becomes eligible
- **WHEN** a previously seen future game reaches its release date without any new upstream metadata update
- **THEN** the next successful scheduled catalog run admits it if its other selection criteria are satisfied

### Requirement: Catalog ingestion is resumable and idempotent
The system SHALL support a paginated initial import followed by scheduled incremental metadata refresh. It SHALL persist progress only after corresponding data is committed and resume interrupted runs without skipping uncommitted records. Equal upstream update timestamps MUST NOT cause games to be skipped. An incomplete or failed run MUST NOT delete existing media or reviews.

#### Scenario: Resume after a failed page
- **WHEN** a run stops before committing a fetched page
- **THEN** the next run can process that page again without duplicate games or lost records

#### Scenario: Multiple pages share an update timestamp
- **WHEN** more than one page of changed games has the same upstream update timestamp
- **THEN** all such games are eligible for processing before the completed refresh checkpoint advances past that timestamp

#### Scenario: Provider or database outage
- **WHEN** an import cannot fetch or commit its next page
- **THEN** committed catalog records remain usable and the incomplete work remains resumable

### Requirement: IMDb imports no longer create games
The system SHALL stop accepting IMDb video-game records into game staging and domain loading while retaining the existing IMDb movie, TV-series, season, and episode behavior and their filtering rules.

#### Scenario: Mixed IMDb dataset
- **WHEN** IMDb input contains video games, qualifying movies, and qualifying television records
- **THEN** no video games are staged or loaded and the supported movie/TV records continue through the existing import flow

### Requirement: Provider access is configurable and bounded
IGDB catalog ingestion SHALL be disabled by default, use server-held Twitch application credentials, renew expired access tokens, and share the configured IGDB request budget with artwork refresh. It SHALL respect the documented maximum of four API requests per second and eight simultaneous requests, handle throttling with delayed retry, and report sanitized operational failures.

#### Scenario: Integration disabled or credentials unavailable
- **WHEN** the integration is disabled or enabled without usable credentials
- **THEN** no unauthenticated import loop runs, existing application functionality remains available, and an enabled misconfiguration is reported without exposing credentials

#### Scenario: Token expiration and provider throttling
- **WHEN** an IGDB request encounters an expired token or a rate-limit response
- **THEN** authentication is renewed when appropriate and bounded retry obeys the shared request budget without advancing uncommitted import progress
