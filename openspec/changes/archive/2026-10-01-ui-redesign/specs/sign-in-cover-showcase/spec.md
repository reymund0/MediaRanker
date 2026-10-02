## Purpose

Give the signed-out screens the Amethyst split layout with a wall of real cover art, without exposing user data.

## ADDED Requirements

### Requirement: Anonymous cover showcase endpoint
The system SHALL provide `GET /api/media/showcase` without authentication, returning at most 20 items containing only a title and a cover image URL. Items SHALL come from catalog media of any type whose cover is ready and unexpired. The selection SHALL be the same for every caller within a UTC day and SHALL be served from an in-memory cache after the first request that day. The endpoint SHALL NOT use review data, register artwork demand or call an external provider.

#### Scenario: Signed-out request
- **WHEN** a client without credentials requests the showcase
- **THEN** the response is 200 with at most 20 items, each having only `title` and `coverImageUrl`

#### Scenario: Same day, same set
- **WHEN** two requests arrive on the same UTC day
- **THEN** both return the same items in the same order

#### Scenario: Not enough ready covers
- **WHEN** fewer than 20 ready covers exist
- **THEN** the endpoint returns the ones available, possibly none, with status 200

### Requirement: Auth screen layout
Sign in, sign up, signup confirmation and password reset SHALL use a split layout: a cover wall with the wordmark and tagline on one side, and the form on the other. The wall SHALL use showcase covers and fall back to typographic tiles when the showcase is empty, slow or fails. Form behavior SHALL be unchanged from `password-management` and existing auth pages.

#### Scenario: Showcase unavailable
- **WHEN** the showcase request fails or returns no items
- **THEN** the wall shows typographic fallback tiles and the form works normally

### Requirement: Auth form polish
Auth forms SHALL label fields above the input, offer show/hide on password fields, and present the local test user only under a "development only" divider when it is available. "Resend code" on signup confirmation SHALL be enabled whenever the username field has a value, including a prefilled one.

#### Scenario: Prefilled confirmation
- **WHEN** the confirmation page opens with a username in the URL
- **THEN** "Resend code" is enabled without editing the field
