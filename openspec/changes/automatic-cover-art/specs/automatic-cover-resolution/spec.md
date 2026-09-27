## Purpose

Supply automatic provider-backed artwork for supported imported media while keeping browsing responsive, provider traffic bounded, and unrelated catalog entries free of artwork lookup work.

## ADDED Requirements

### Requirement: Artwork uses authoritative provider identity
The system SHALL resolve IMDb movies through TMDB using their IMDb ID and IGDB games through their stored IGDB identity or imported cover reference. Artwork-provider identity MUST NOT replace the identity used by catalog imports. The system SHALL use provider-hosted HTTPS images and SHALL NOT download or upload cover files to application-owned storage.

#### Scenario: Movie resolution
- **WHEN** a demanded IMDb movie resolves to a TMDB movie with a poster
- **THEN** the poster reference is retained for automatic display while the movie's catalog identity remains IMDb

#### Scenario: Imported game reference
- **WHEN** a demanded IGDB game already has a fresh imported cover reference
- **THEN** its image URL is available without a separate title search or cover-discovery API request

#### Scenario: Unsupported or unlinked media
- **WHEN** a manually created record without provider identity, or a book, album, or concert is displayed
- **THEN** no speculative provider search occurs and its cover is a placeholder

### Requirement: Television shares a series poster
The system SHALL resolve imported TV artwork using the ancestor series IMDb identity and use that poster for the series, its seasons, and its episodes. All descendants SHALL observe the same canonical resolution state, freshness, removal, and retry outcome. It SHALL NOT treat a season's stored IMDb ID as an independent season identity or request individual episode stills in this version.

#### Scenario: Browse episodes from one series
- **WHEN** multiple episodes or seasons from the same series need artwork
- **THEN** they share one series-level resolution and show that series poster when available

#### Scenario: Episode has no valid series ancestry
- **WHEN** an episode cannot be associated with a known imported series
- **THEN** it remains a placeholder without making an incorrect movie or episode lookup

### Requirement: Artwork lookup is driven by user demand
The system SHALL register missing or stale artwork work only for authorized media returned by browse/search/review flows or referenced by a successfully saved review. It SHALL return those responses without waiting for provider HTTP calls. It MUST NOT enqueue all database search matches or schedule an entire catalog artwork backfill. Fetching cover-reference metadata as part of an IGDB catalog page is permitted and is distinct from downloading images.

#### Scenario: Browse a page from a large catalog
- **WHEN** a user receives one page of supported titles with missing artwork
- **THEN** only the returned titles and their canonical series identities become eligible for background resolution

#### Scenario: Slow provider during review creation
- **WHEN** a user saves a valid review while the image provider is unavailable
- **THEN** the review succeeds independently and missing artwork remains available for later background retry

#### Scenario: Repeated and concurrent demand
- **WHEN** multiple requests demand artwork for the same canonical title
- **THEN** resolution work is deduplicated and provider calls are not multiplied by the number of viewers

### Requirement: Resolution outcomes survive retries and restarts
The system SHALL retain successful references, known no-image results, pending work, and retryable failures. It SHALL distinguish absence of an image from authentication, timeout, throttling, and server failures; retry transient failures with bounded backoff; and recover interrupted work after restart. No-image results SHALL suppress repeat lookups until their retry time.

#### Scenario: Known absent poster
- **WHEN** a provider successfully reports no matching item or no image
- **THEN** the title remains a placeholder and repeated browsing before the configured retry time does not repeat the provider lookup

#### Scenario: Transient failure
- **WHEN** a provider times out, returns HTTP 429, or returns a server error
- **THEN** work is retained for delayed retry without being classified as a permanent missing image

#### Scenario: Worker stops during a lookup
- **WHEN** a worker stops after claiming work and before completing it
- **THEN** a later worker can recover the unfinished lookup without leaving the title permanently pending

### Requirement: Cached artwork has bounded freshness
The system SHALL expire successful and negative lookup metadata according to provider-specific policies, defaulting to 30 days for successful references and 7 days for no-image results for both providers. Freshness SHALL begin at the actual successful provider fetch, including an IGDB catalog fetch, and MUST NOT be extended by rereading local staging data. Expired references SHALL be withheld until refreshed; refreshed or removed artwork SHALL be reflected consistently on later reads. A demanded expired game reference SHALL refresh by IGDB ID. Dormant records SHALL NOT trigger provider lookups solely because time has passed. TMDB-derived metadata SHALL NOT remain cached beyond six months.

#### Scenario: View an expired cover
- **WHEN** a user displays a title whose cached artwork has expired
- **THEN** the old reference is not served and one refresh is scheduled for that demanded title

#### Scenario: Provider removes a cover
- **WHEN** a refresh reports that the previously used cover no longer exists
- **THEN** the obsolete reference is removed and the title uses a placeholder with negative-result retry behavior

### Requirement: Secrets and provider controls stay on the server
The system SHALL keep TMDB and Twitch credentials out of client responses, image URLs, committed files, and logs. Provider clients SHALL enforce configured rate/concurrency budgets and use expected provider hosts. Disabled or misconfigured artwork integrations SHALL leave browsing/reviewing functional without an uncontrolled retry loop.

#### Scenario: Provider integration disabled
- **WHEN** a supported title has no usable cover and its provider integration is disabled
- **THEN** the UI receives a non-pending placeholder state and no provider lookup is attempted

#### Scenario: Render a resolved image
- **WHEN** a cover URL is returned to a client
- **THEN** it is an HTTPS provider image URL with no API key, client secret, or bearer token
