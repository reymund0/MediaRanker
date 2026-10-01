# Automatic Cover Experience Specification

## Purpose

Let users browse, edit, and review media without supplying artwork, with consistent automatic covers, accessible fallbacks, and clear attribution to image providers.

## Requirements

### Requirement: Media workflows do not request uploaded covers
The application SHALL remove manual cover upload controls and cover-upload fields from media and collection edit flows and SHALL retire the cover-upload API endpoints. Users SHALL be able to create/edit media and save reviews without selecting a file. Ordinary metadata changes MUST NOT clear or replace automatically associated artwork.

#### Scenario: Edit metadata without a file
- **WHEN** a user edits a supported imported title or date and saves
- **THEN** the edit succeeds without an upload and the existing provider identity and automatic artwork association remain intact

#### Scenario: Old upload endpoint is called
- **WHEN** a client calls a retired cover-upload endpoint
- **THEN** an authenticated request receives HTTP 404 without issuing a presigned upload URL or creating a cover upload

#### Scenario: Create a manually entered title
- **WHEN** a user creates a title without external provider identity
- **THEN** creation succeeds without an upload and the title displays a placeholder

### Requirement: Automatic artwork is consistent across display surfaces
The application SHALL expose the same effective cover for a title in media browsing, unreviewed-media search, review lists/cards, and collection responses where present. Images SHALL load only for content being displayed, using appropriately sized provider variants; unsupported, missing, or failed images SHALL show a stable accessible placeholder.

Cover responses SHALL retain their existing URL field and expose a consistent status: `ready` for a usable fresh URL, `pending` for runnable or in-progress work, `missing` for a cached no-image result, `failed` for a delayed/exhausted failure, `disabled` for unavailable integration configuration without a usable reference, or `unsupported` for records without supported identity. Only `pending` SHALL request automatic client polling. Non-ready statuses SHALL have no usable image URL.

#### Scenario: Same title on multiple surfaces
- **WHEN** a resolved title appears in media search and a saved review
- **THEN** both display the same provider-backed cover rather than depending on an uploaded file

#### Scenario: Browser cannot load an image
- **WHEN** the provider image request fails
- **THEN** the UI replaces the broken image with a placeholder without blocking interaction, repeated automatic image-error loops, or an upload prompt

### Requirement: Visible pending artwork updates without manual reload
The application SHALL refresh pending artwork every two seconds for at most 30 seconds per active view activation. It SHALL stop automatic polling when no visible result is pending, when its refresh budget is exhausted, when the document becomes hidden, or when the view is unmounted. Ordinary view activation can start a new bounded window. Refresh SHALL preserve search filters, pagination, unsaved form values, review order, and user input.

#### Scenario: Artwork resolves after the initial response
- **WHEN** a visible pending title resolves during the active view's refresh window
- **THEN** its placeholder is replaced by the cover without requiring a page reload

#### Scenario: No image or retry is delayed
- **WHEN** the server reports no image, a disabled integration, or a failure whose retry is outside the active refresh window
- **THEN** the placeholder remains usable and the client does not poll indefinitely

### Requirement: Providers receive visible attribution
The application SHALL provide an accessible About/Credits location with the approved TMDB logo and required endorsement disclaimer and a visible IGDB source link. Attribution SHALL describe the actual providers used without implying endorsement.

#### Scenario: User opens credits
- **WHEN** the user opens the application's credits from navigation
- **THEN** the configured providers are identified and TMDB's required branding and disclaimer are present

### Requirement: Existing authorization boundaries remain effective
Artwork demand and refresh SHALL use existing authenticated application access and SHALL NOT reveal another user's reviews or allow arbitrary external image URLs or provider queries from clients.

#### Scenario: Unauthorized review refresh
- **WHEN** an unauthenticated client or a user without access attempts to refresh another user's review data
- **THEN** the existing authorization boundary is enforced and no review data is exposed

### Requirement: Development verification has a reusable local test user
The application SHALL offer an explicitly enabled local test login in development on localhost with one stable test identity. It SHALL use ordinary user-scoped APIs and SHALL clear its tab session on logout. This mode SHALL NOT register or accept test authentication in production, with its setting disabled, or from a non-loopback connection. Cognito SHALL remain the normal authentication mechanism outside that explicitly enabled local mode.

#### Scenario: Local developer selects the test user
- **WHEN** both local development settings are enabled and the developer chooses the test login on localhost
- **THEN** browsing and reviewing use the fixed test identity without requiring Cognito credentials, refresh preserves the tab session, and logout removes it

#### Scenario: Test identity is presented outside local development
- **WHEN** a test credential is presented with the flag disabled, in production, or from a non-loopback connection
- **THEN** it does not authenticate and cannot access protected API data
