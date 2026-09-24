## Purpose

Provide explicit, finite and observable initial metadata ingestion with recoverable provider-specific progress, while preserving demand-driven artwork and requiring measured readiness before larger imports.

## ADDED Requirements

### Requirement: Explicit finite bootstrap activation
The system SHALL keep bootstrap disabled by default and support an explicitly selected immediate backend bootstrap session for one catalog provider at a time. It MUST validate effective activation flags and positive finite limits before doing work, rejecting different values supplied for both legacy and canonical IGDB enable keys. During bootstrap both catalog schedules SHALL be suppressed. A capped, stopped or failed session MUST NOT automatically renew its allowance or fall through to either scheduled catalog ingestion path in the same process. Successful bootstrap SHALL release both schedules for their next ordinary enabled run; independently enabled artwork SHALL remain available.

#### Scenario: Default startup
- **WHEN** bootstrap is not explicitly selected
- **THEN** no immediate bootstrap starts and existing disabled/scheduled behavior is preserved

#### Scenario: Persisted bootstrap activation is insufficient
- **WHEN** bootstrap activation and finite session allowances are not explicitly supplied on the current launch command line
- **THEN** persisted configuration or inherited environment alone cannot activate bootstrap, and the operating procedure forbids automatic replay of a saved bootstrap command

#### Scenario: Invalid or conflicting activation
- **WHEN** effective provider flags conflict, both catalogs request bootstrap, or any required bound is invalid
- **THEN** activation fails with a clear sanitized diagnostic before catalog work begins

#### Scenario: Session reaches its bound
- **WHEN** a session reaches a configured session request, time or admission limit
- **THEN** it stops starting catalog work, preserves committed progress and reports its stop reason without creating a new allowance

#### Scenario: Work unit reaches its bound
- **WHEN** a work unit reaches its page, request, time or admission limit while session allowance remains
- **THEN** it yields before another bounded unit and preserves the cumulative session counters and deadline

### Requirement: Provider-specific stop and recovery
The system SHALL cancel in-flight operations on explicit stop or deadline and preserve committed state. IGDB SHALL resume its persisted scan bounds/cursor and pending admission. IMDb SHALL report that interrupted work requires complete feed replay, with stable identities on replay. Process-local session counters MUST be identified as non-durable; the supported bootstrap host MUST have automatic restart disabled, and a new process launch MUST be treated as a new deliberate allowance.

#### Scenario: IGDB interruption after staging commit
- **WHEN** execution stops after a page and cursor commit but before admission
- **THEN** the next deliberately started session discovers the committed backlog and resumes without losing or duplicating identities

#### Scenario: IMDb interruption
- **WHEN** an IMDb session stops before complete ingestion or during cleanup/loading
- **THEN** committed data remains and the next deliberate session replays all required feeds without claiming byte or row cursor recovery

#### Scenario: No crash quota guarantee
- **WHEN** an operator prepares a bootstrap host with automatic process restart
- **THEN** the bootstrap operating procedure rejects that profile until restart is disabled or a separately approved durable quota design exists

#### Scenario: Deliberate relaunch while prior lease is held
- **WHEN** the previous crashed import lease has not expired at the next bootstrap launch
- **THEN** the session reports lease busy and its expiry when known, makes zero provider sends and requires a deliberate retry after expiry

### Requirement: Complete request accounting and shared provider protection
The system SHALL enforce an import-attributable HTTP attempt allowance before actual sends, counting tokens, discovery, pagination, empty terminal reads and retry/resend attempts. Import and artwork SHALL share provider pacing, concurrency and cooldown. Aggregate artwork traffic MUST be reported separately from the import allowance rather than falsely presented as bounded by it. Retries MUST consume the original session allowance and time.

#### Scenario: Token and unauthorized retry consume allowance
- **WHEN** import requires token issuance, discovery and a refreshed-token resend
- **THEN** every actual HTTP attempt counts and no import send starts after its allowance is exhausted

#### Scenario: Shared token renewal attribution
- **WHEN** import and artwork concurrently require token renewal
- **THEN** the initiating flow accounts for the token send exactly once, waiters do not duplicate-count it, and independent artwork does not inherit an exhausted import allowance or cancellation context

#### Scenario: Shared cooldown during mixed traffic
- **WHEN** a provider enters authentication, throttle or classified transient token-failure cooldown
- **THEN** sibling import/artwork requests respect that cooldown, repeated failures do not cause rapid token attempts, and healthy independent provider work can progress

#### Scenario: Bootstrap encounters a locally established cooldown
- **WHEN** a shared cooldown rejects the next bootstrap provider call before HTTP is sent
- **THEN** the session reports provider cooldown and retry-after, stops without spinning, preserves committed admission, and does not charge an HTTP attempt for that rejection

#### Scenario: Interactive artwork competes with bootstrap
- **WHEN** bounded catalog work and demanded artwork share a healthy provider
- **THEN** their combined traffic respects provider pacing/concurrency and both make measured progress without a catalog-wide artwork queue

### Requirement: Independently bounded IGDB admission
The system SHALL bound staging-to-domain work separately from network pages and HTTP attempts, and SHALL allow committed eligible staging to be admitted without successful upstream HTTP. It MUST preserve fixed scan bounds, atomic page/cursor progress, ownership fencing and original cover freshness. Upstream completion MUST NOT be reported as catalog readiness while eligible admission remains.

#### Scenario: Large staging backlog with no new upstream data
- **WHEN** eligible committed staging exceeds the configured admission limit
- **THEN** only bounded work is admitted and the remaining backlog is reported and rediscovered by later work units or sessions

#### Scenario: Provider unavailable but staging ready
- **WHEN** committed staged rows are eligible while the upstream provider is unavailable
- **THEN** bounded local admission can progress without performing successful provider discovery first

#### Scenario: Lost lease or concurrent cover update
- **WHEN** ownership expires/is replaced or an artwork writer changes the same canonical cover during admission
- **THEN** stale work cannot overwrite the new owner or newer artwork; the affected transaction rolls back or safely retries within its original bounds, with staging recoverable

#### Scenario: Persistent failed admission batch
- **WHEN** an admission batch fails non-retryably or exhausts bounded concurrency retries
- **THEN** the session stops with an admission-blocked category and sanitized batch identity/count, retains staged rows, and neither retries that prefix in further units nor claims catalog readiness

#### Scenario: Bootstrap completion with future releases
- **WHEN** the scan is exhausted and no currently eligible staged work remains
- **THEN** catalog readiness can be reported while future-release/ineligible rows remain staged for their existing eligibility rules

#### Scenario: Completed scan with remaining admission on relaunch
- **WHEN** bootstrap mode is deliberately launched after its scan was persisted complete
- **THEN** it drains only eligible staged work and probes initial-scan readiness without starting or resuming an incremental window, preserving any pending incremental cursor for the normal schedule and reporting it separately

#### Scenario: Large scheduled incremental window
- **WHEN** a scheduled IGDB invocation begins or resumes a potentially catalog-sized incremental window
- **THEN** finite HTTP, elapsed-time and admission limits apply alongside its page budget, and unfinished work resumes on later scheduled runs

#### Scenario: Scheduled mode with incomplete bootstrap scan
- **WHEN** an enabled scheduled IGDB invocation finds the bootstrap scan incomplete
- **THEN** it preserves existing behavior by advancing that scan under finite invocation limits and defers remaining work to later days

#### Scenario: Scheduled IGDB admission is blocked
- **WHEN** a scheduled IGDB invocation encounters a non-retryable admission failure or exhausts its bounded concurrency retries
- **THEN** it reports admission blocked and suppresses further IGDB scheduling in that process pending operator remediation and restart, without suppressing unrelated scheduled catalogs

### Requirement: Complete IMDb ingestion before cleanup and loading
The system SHALL require successful completion of every required IMDb feed and batch before cleanup or domain loading. Cancellation, failed batches, malformed required data, invalid structure and truncated/decompression failures MUST prevent completion and destructive cleanup. Intentional business-rule exclusions SHALL remain distinct from malformed input. Downloads, memory, temporary disk, statements and admission work MUST have explicit finite bounds.

The initial malformed-input policy SHALL tolerate zero structural or required-value errors, while preserving valid optional null markers, the existing episode-number unknown sentinel, and intentional selection filters. A rejected real feed MUST be reported as blocked rather than silently increasing tolerance. IMDb SHALL operate under the documented single-writer process constraint; cross-process writer fencing is not claimed.

#### Scenario: Failed middle basics or episodes batch
- **WHEN** a required batch fails after earlier batches committed
- **THEN** the run is incomplete, cleanup/domain loading do not begin, and a later full replay remains duplicate-safe

#### Scenario: Incomplete ratings feed
- **WHEN** ratings processing stops or fails before validated EOF
- **THEN** unseen ratings are not deleted using that partial invocation's cutoff and domain loading is not started

#### Scenario: Ratings cleanup spans work units
- **WHEN** a successful single-writer IMDb invocation removes stale ratings across multiple units
- **THEN** every delete uses the same cutoff consistent with persisted update timestamps, and a later replay uses its own new cutoff

#### Scenario: Bounded loading preserves hierarchy
- **WHEN** a complete IMDb ingestion is loaded in bounded units
- **THEN** series precede seasons/episodes and each selected season's aggregate reflects all its qualifying episodes while affected output remains within the transaction cap even for very large series

#### Scenario: Download exceeds allowance
- **WHEN** compressed bytes, disk usage or download/processing elapsed time reach their configured bound
- **THEN** the run stops, temporary resources are cleaned up, committed staging remains, and the result does not claim completed ingestion

#### Scenario: Valid optional null versus malformed required value
- **WHEN** a feed contains a documented optional null or an invalid required value
- **THEN** the valid null retains its existing mapping while the required-value error makes the feed incomplete under the strict policy

#### Scenario: Transfer ends on a valid row boundary but lacks integrity proof
- **WHEN** parsed rows are well formed but the compressed transfer length mismatches its advertised length or complete gzip integrity/trailer validation fails
- **THEN** the feed remains incomplete and no cleanup or domain loading begins

#### Scenario: Live IMDb mode has no calibrated finite profile
- **WHEN** scheduled or bootstrap IMDb live execution is enabled without an explicit operator-reviewed calibrated whole-invocation profile
- **THEN** validation rejects activation rather than using unbounded or small-fixture-derived fallback limits

#### Scenario: Scheduled IMDb fails
- **WHEN** scheduled IMDb reaches a configured bound or transient fault
- **THEN** it reports incomplete and can retry only at a later scheduled invocation; a strict malformed/integrity rejection instead reports feed blocked and suppresses further IMDb scheduling in that process pending remediation and restart

### Requirement: Bulk metadata preserves demand-driven artwork and selection
Bootstrap SHALL preserve existing IMDb movie/TV selection and IGDB released-game selection. It MUST NOT import IMDb games, enqueue artwork for the entire catalog, request TMDB artwork merely because metadata was imported, or download image binaries. Fresh IGDB cover references acquired with metadata SHALL be reused without another discovery request; local replay MUST NOT renew their freshness. Credentials SHALL remain server-side and browsers SHALL receive resolved image URLs.

#### Scenario: Fresh imported game is browsed
- **WHEN** a game with a fresh imported cover reference is returned to an authorized browse/review flow
- **THEN** the resolved CDN URL is available with zero additional provider discovery calls

#### Scenario: Missing cover after metadata import
- **WHEN** metadata is admitted without artwork
- **THEN** no artwork request is created until the existing authorized demand flow selects that title

### Requirement: Honest progress and measured bulk readiness
The system SHALL distinguish upstream completion, remaining eligible admission, paused/failed sessions and catalog readiness in sanitized progress output. It SHALL report committed/affected counts, elapsed time, actual HTTP attempts, effective limits and stop reasons without secrets or false inserted-row/percentage claims. Before larger live imports, the workflow MUST have data/performance reviews and representative isolated measurements of SQL amplification, query plans, transaction duration, memory/disk, lease headroom, recovery and mixed import/artwork traffic.

#### Scenario: Page committed but domain load deferred
- **WHEN** a committed page remains partly unadmitted at session end
- **THEN** progress reports both durable upstream progress and remaining admission rather than full catalog completion

#### Scenario: Readiness evidence is incomplete
- **WHEN** only static review, small samples or functional tests are available
- **THEN** bulk readiness remains unproven and no larger live profile is activated automatically

#### Scenario: IMDb full-feed scale is unmeasured
- **WHEN** full-size measurements from separately authorized bounded calibration downloads of all required IMDb feeds are unavailable or incomplete
- **THEN** IMDb whole-import readiness remains unproven even if smaller fixtures pass, and only the disabled implementation and outstanding gate can be delivered

#### Scenario: Retained import failure logs
- **WHEN** an IMDb batch, parser or load statement fails
- **THEN** logs contain sanitized counts and error context without full SQL, raw provider rows or field values

#### Scenario: Measurements support a larger profile
- **WHEN** comparable baseline/candidate measurements satisfy recorded correctness, resource, latency, request and lease gates and specialist findings are reconciled
- **THEN** a finite proposed operating profile and forecast can be presented for explicit live-run authorization
