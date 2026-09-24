## Context

See [proposal.md](proposal.md) for motivation. The prerequisite is accepted checkpoint `346ccc6c8e446b69cd235de3ef9e5a87904e63c6`; its review waiver remains closed. This change is a proposal, not evidence that bulk throughput has been measured.

Current controls and gaps:

| Path | Reusable behavior | Gap for bootstrap |
| --- | --- | --- |
| `IgdbImportJob` / `BaseJob` | Scoped daily execution, cancellation, logs | No immediate finite bootstrap session |
| `IgdbImportService` | Fixed bootstrap maximum ID; fixed incremental window and overlap; atomic staging/cursor commits | Page budget omits discovery, tokens and retries; network failure prevents admission |
| `IgdbImportSqlProvider` | Durable lease, version/token fencing, 500-row keyset admission, freshness checks | Admission loops over all eligible backlog; upstream completion precedes admission completion |
| `IgdbClient` / `IgdbRequestLimiter` | Singleton token renewal and process-wide API pacing, concurrency and cooldown | Token HTTP is outside API acquisition; several token failure paths lack shared cooldown |
| IMDb import | Ratings, basics, episodes; download-to-temp gzip; callback batches; idempotent SQL | No durable stage cursor; basics/episodes swallow failed batches; ratings success alone gates loading |
| IMDb load | Movies, series, seasons, episodes in dependency order | Whole-set statements with 30-minute command timeout |
| Artwork | Demand registration, provider references, leases, expiry, shared TV posters | Per-title SQL on reads; review lists are unpaged; bulk-scale query costs unmeasured |

## Goals / Non-Goals

**Goals:** Extend existing jobs and provider services with finite, observable bootstrap execution; prove safe recovery and bound provider/database work; retain interactive artwork progress.

**Non-Goals:** A general job framework, durable operator-session journal, hot configuration/control API, multi-process bootstrap, or new schema. IMDb refresh remains periodic dataset replay; it is not relabeled as an incremental API feed. Existing selection rules remain: configured IMDb movie/TV vote thresholds (currently 1,000), adult/future/pilot rules, and released IGDB main games/remakes/remasters. This is not an import of every excluded title.

## Decisions

### 1. Explicit bootstrap profile in the existing hosted jobs

Add disabled-by-default bootstrap options to the provider jobs. When explicitly selected at process launch, run immediately in fresh scopes, using bounded work units and a short yield between units. Keep orchestration in the jobs and domain work in existing services/providers. Avoid a shared `BaseJob` behavior change affecting unrelated jobs; a small protected scheduling hook is acceptable only if it preserves their current behavior and tests.

Bootstrap activation and its finite session allowances must be explicit command-line inputs on that launch. Reject activation supplied only through persisted appsettings, user secrets or inherited environment; such configuration may retain disabled defaults. Validate argument presence/source as well as values. The operating procedure forbids saving the bootstrap command into an automatic launch/restart profile: source validation does not itself prove operator intent or prevent a supervisor from replaying the same command. Ordinary scheduled profiles remain configuration-driven.

Run one catalog bootstrap provider at a time in the first operator profile; ordinary demand-driven artwork can run in that same process. Reject simultaneous IMDb and IGDB bootstrap configuration and suppress the other catalog's scheduled execution during bootstrap. IMDb requires a single catalog writer throughout ingestion, cleanup and loading. This is an operator-enforced single-process constraint, not cross-process fencing; a second IMDb writer (including an old application revision) is unsupported. No controller-triggered import or new UI is needed. Before binding the aliased IGDB properties, inspect the effective raw `IConfiguration` values for `Enabled` and `ImportEnabled`: if both exist and differ, fail clearly. If only one exists use it; if both agree accept that value. Final bound options alone cannot reveal a conflict because both setters target the same property. Document setting both keys consistently when lower-precedence configuration contains the legacy alias.

A session is finite and scoped to one deliberately launched process. Cap/time/fault termination pauses both catalogs for the remainder of that process, including both scheduled import paths; report that release-day admission is paused too. It must not repeatedly renew the allowance, fall through into daily imports, or self-restart. A successful bootstrap releases suppression for both catalogs, each returning to its next normal scheduled time only if enabled: IGDB incremental windows; IMDb full dataset refresh. Missed schedules are not replayed immediately. Artwork remains independently enabled and running.

Stopping uses host cancellation. Resume requires a deliberate new launch with an explicit new allowance: IGDB resumes its durable cursor/backlog, whereas IMDb replays all required feeds. Session request counters are not durable. The initial bootstrap profile therefore requires a manually supervised host with automatic restarts disabled; it is not suitable for a service supervisor that replenishes budgets on crash. This limitation avoids inventing a persistence layer. If crash-persistent cumulative quotas become required, amend scope before implementation.

Alternatives considered: merely raising daily `PageBudget` leaves staging, HTTP overhead and failures unbounded; a persistent control UI/job store adds unsupported scope; a separate worker process would break shared in-process artwork throttling unless all provider traffic moved with it.

### 2. Distinguish budgets and measure every outbound attempt

Keep per-provider pacing/concurrency shared across importer and artwork. Extend the existing IGDB send paths so token requests also observe the shared pacing/cooldown without nested acquisition deadlock. Before each actual send, reserve an import attempt atomically; count game-type pages, maximum-ID discovery, game pages, terminal empty pages, token acquisition, 401 refresh/resend and failed attempts. A cached token or rejected local cooldown is not an HTTP attempt. Disable hidden uncounted HTTP retries/redirects or account for them explicitly at the transport boundary.

For a shared token-refresh race, charge the actual initiating flow exactly once. An import awaiting an artwork-initiated token refresh does not duplicate-count that send; neither an exhausted import allowance nor its cancellation context may be inherited by an independent artwork call. Test warm/cold mixed-flow renewal races and report aggregate token sends alongside attributed sends.

There are two distinct measurements: the import session's HTTP attempt cap and aggregate process-wide provider traffic. Artwork has its existing demand/attempt bounds and shared rate/concurrency limits; independently enabled artwork traffic is not falsely described as part of an absolute host HTTP cap. Supervised live metadata-only calibration keeps artwork lookup disabled when an absolute request total is required; mixed-load readiness is established with fake providers.

Use independent limits for work-unit pages, work-unit HTTP sends, admission rows/batches, statement duration, unit duration, session duration, session HTTP attempts and total session admission. A unit limit ends that unit; after yielding, another unit may start within the same remaining session allowance. A session limit ends the session. Unit transitions never reset session counters or deadlines. Waiting for a limiter, response bodies, retry delays and database work count toward elapsed time. A linked deadline token cancels in-flight work; transaction rollback and lease release can overrun briefly and must be measured/reported rather than promised as a hard process-kill deadline.

Keep one catalog request active at a time initially. Yield between IGDB units so artwork can acquire the shared limiter. IMDb ingestion units are callback batches within one open feed stream; cleanup/load units are bounded SQL statements. IMDb has its own yield setting, initially zero additional delay, because it does not share the IGDB limiter. Carry invocation success/counters across fresh database scopes without reopening or replaying the stream at each unit. Include any later measured IMDb yield in its whole-invocation allowance. Do not introduce a priority queue without starvation evidence. Stop the bootstrap on authentication/throttle responses, lost lease or an unclassified fault; report cooldown/retry-after for the next deliberate launch. Bound transient retry and concurrency-conflict recovery within the original budgets, never by recursively starting a fresh session. Verify token 400/5xx/timeout/network/invalid-payload paths with fake HTTP; apply a shared bounded cooldown where repeated failure would otherwise trigger rapid sibling token attempts. Do not infer real provider failure incidence from those fixtures.

A local shared-cooldown rejection is also an immediate `provider cooldown` stop for a bootstrap session, with remaining retry-after and zero charge for the unsent request. Do not loop on local rejections or sleep through repeated units. Admission performed before that attempted request stays committed; further admission waits for a deliberate next launch. An ordinary scheduled invocation ends on cooldown and can retry at its next daily schedule within new invocation bounds.

Provisional **local measurement** profile, not enabled defaults or authorization for live calls:

| Control | Starting value |
| --- | --- |
| IGDB page size / pages per unit | 100 / 5 |
| Import HTTP attempts per unit / session | 20 / 100 |
| Unit / session deadline | 60 seconds / 15 minutes |
| Admission rows per unit / session | 500 / 5,000 |
| Shared requests per second / concurrent requests | 1 / 1 |
| IGDB HTTP timeout / lease duration / between-unit yield | 10 seconds / 120 seconds / at least 1 second |
| IMDb staging batch / load and cleanup output rows per unit | 1,000 / at most 1,000 |
| Database statement target | 15 seconds, with explicit cancellation/rollback reserve |

These are calibration limits. The eventual larger profile must be derived from measured rows/second, overhead, SQL cost and resources, with a finite session bound and forecast of remaining work. Sparse IDs are not a row-count denominator; do not report `cursor/maxId` as percentage complete. IMDb requires a separate measured whole-import deadline and compressed byte/disk ceilings; the 15-minute IGDB value is not an IMDb completion promise.

### 3. IGDB fetch and admission advance independently

Reuse the persisted bootstrap bounds, incremental windows, lease token/version and page/cursor transaction unchanged in meaning. Do not advance the cursor on failed page commit or reset an unfinished run when a session ends. Reject repeated/non-increasing pages as an observable failure rather than cycling inside the request allowance.

Drain a bounded amount of pending eligible staging before more network ingestion and after each successful page/unit, using the known supported game-type names already stored in staging. Admission must also work while upstream is unavailable, including release-day admission without provider changes. If backlog exceeds the unit allowance, apply backpressure and spend later units draining it before fetching more pages.

The existing eligibility anti-join rediscovers pending rows on restart; no durable admission cursor is needed for correctness. Preserve ID ordering within each pass, fixed fetched timestamps, canonical cover identity, and freshness/version guards. A bounded row result does not bound rows examined, so query plans and timeouts are separate gates. Refresh a conflicted context before bounded retry; after retry exhaustion defer visibly with staging intact. Do not weaken concurrency checks.

Any non-retryable admission failure or exhausted conflict retry stops the session as `admission blocked`, including the sanitized failing batch ID range/count and failure category. Do not retry the same prefix in another unit, skip or quarantine it silently, or claim readiness. Committed staging remains; an operator must resolve the cause before a deliberate retry, which can encounter it again. This fail-stop policy deliberately favors visibility and integrity over draining later rows behind a bad batch; row isolation/quarantine is outside scope.

Report `upstream complete`, `admission remaining`, `session paused`, and `catalog ready` separately. Bootstrap is ready only after the upstream scan is complete and no currently eligible staged work remains at the final bounded probe; future-release/ineligible rows are not a backlog failure. New provider updates after the fixed scan are handled by the ordinary incremental window with its existing overlap.

If a deliberate bootstrap launch sees `BootstrapCompleted=true`, it performs admission-only draining and a readiness probe; it never starts a new incremental window under bootstrap mode. After that completes, the next scheduled run starts/resumes incremental work. Every scheduled IGDB invocation also uses finite HTTP, time and admission limits, retaining its configured page budget and durable window/cursor; cap exhaustion defers remaining work to the next scheduled run. This includes a potentially large first incremental window since `BootstrapStartedAt`. Bootstrap-only session termination suppresses schedules as described above; an ordinary scheduled run's bounded completion does not permanently disable future days.

Mode/state decisions:

| Mode and persisted IGDB state | Network work | Admission and next action |
| --- | --- | --- |
| Explicit bootstrap; scan incomplete | Start/resume the existing bounded bootstrap scan | Bounded admission/backpressure; repeat units within session allowance |
| Explicit bootstrap; scan complete, idle | None | Admission-only then readiness; next enabled daily schedule starts incremental work |
| Explicit bootstrap; scan complete, active incremental window | Leave that window/cursor untouched; do not resume it in bootstrap mode | Admission-only then readiness for the completed initial scan; report incremental pending; next daily schedule resumes its fixed window |
| Scheduled; scan incomplete | Preserve existing compatibility: one bounded invocation advances bootstrap | Bounded admission; continue unfinished scan on a later day |
| Scheduled; scan complete, active incremental window | Resume the persisted fixed window | Bounded admission; defer remaining work to later days |
| Scheduled; scan complete, idle | Start one bounded incremental window | Bounded admission including release-day rows |

For scheduled IGDB work, a non-retryable admission error or exhausted concurrency retry reports `admission blocked` and suppresses further IGDB scheduling for the current process until operator restart after remediation; it does not automatically retry the same prefix each day. Other catalog schedules remain independent in ordinary scheduled mode. A scheduled request/time cap or provider auth/throttle/cooldown stop ends only that invocation, reports its reason and retries no earlier than the next daily schedule. Bootstrap faults/caps keep the stronger both-catalog pause defined in Decision 1.

Lease checks must use current time around ownership-sensitive operations, account for limiter and DB waits, and retain token/version fencing on commits and release. Avoid renewing from an old invocation timestamp. Verify takeover and blocked-transaction behavior; measure the longest interval between renewals and reserve enough time for commit/rollback. Do not claim a lease duration greater than HTTP timeout alone guarantees safety.

An unavailable lease at session start produces `lease busy` with its expiry when known, zero provider sends and no automatic wait/restart. The operator can deliberately launch after expiry. Cancellation-safe best-effort release uses a separate bounded cleanup token; an abrupt crash still relies on lease expiry. This behavior is explicit rather than a quiet skipped/completed result.

### 4. IMDb complete ingestion gate and honest replay

Retain the ratings → basics → episodes ingestion order and movies → series → seasons → episodes domain order. Reset run counters and success evidence once per invocation, carrying them across per-unit database scopes. The stream-owning orchestrator is invocation-scoped; only database providers/contexts are resolved per unit. Every required feed must reach validated EOF with all intended batches committed before cleanup and domain loading can begin. Propagate cancellation and batch/database/decompression errors; distinguish intentional business-rule filtering from malformed required fields. Malformed structural/required-data rows make the run incomplete and prevent cleanup/loading; report sanitized counts, not raw rows.

The initial malformed-input policy is deliberately strict: zero tolerated header/column/decompression errors or invalid required values. Basics require a valid title ID, nonempty title/type and valid adult flag; present year/runtime values must parse, while documented null markers remain valid. Episodes require valid episode/parent IDs; season/episode numbers accept the existing null-to-unknown sentinel policy, otherwise must parse. Ratings require a valid ID, numeric rating in range 0–10 and a nonnegative integer vote count. Optional nulls and legitimate business-rule exclusions do not count as malformed. Unknown title types can remain intentionally excluded. This policy is a conscious decision, not a claim the real feeds have zero defects: calibration must report any malformed incidence, and any tolerated-row policy would require an explicit amendment before loading those feeds.

Move destructive staging cleanup (including stale ratings) behind this all-feed success gate. Sample one ratings run-start cutoff from the database clock used by ratings `updated_at` and retain it unchanged across every cleanup unit of that fully successful invocation. Each delete must retain `updated_at < cutoff` in the write predicate, not merely key selection. Partial snapshots must never evict unseen ratings. A fresh replay receives a new cutoff. Cleanup failures prevent advancing to domain load; successful cleanup/load units remain replayable if later units fail. Domain loading is not whole-catalog atomic; already committed units remain visible and later replay finishes them safely.

Use the existing temporary-file model with independent compressed-download bytes, disk allowance and elapsed deadline checks, bounded decompression/row processing, and cleanup on every exit. Capture response validators/length and stage counts when available for diagnostics; three independently published feeds are not a transactional source snapshot. No byte-range resume, durable file manifest, or false immutable cross-feed snapshot guarantee. Stopping requires a full new download/parse replay; choose a measured allowance sufficient for an entire successful IMDb invocation. Repeatedly stopping before the first complete ingestion cannot accumulate an IMDb completion checkpoint.

Validated EOF requires a valid expected header, transport completion with actual compressed bytes matching advertised Content-Length when supplied, and complete validated gzip integrity/trailer consumption for the supported format. A parser merely returning its last full line is insufficient. Verify the runtime's integrity behavior using truncated-mid-member, row-boundary truncation, missing/bad trailer and invalid/trailing-content fixtures; add bounded validation if the runtime alone accepts those cases. Reject unverifiable/incomplete gzip members. Missing Content-Length alone need not fail a fully integrity-validated transfer, but it does not waive byte/time/disk caps or gzip validation. Document that these checks detect incomplete/corrupt transfer, not a logically incomplete but internally valid source publication.

IMDb whole-import readiness requires a separate, explicitly authorized full-feed calibration after the local fixture gates and specialist review: one bounded download of each required feed, with an exact send/byte/disk/time allowance and verified target. Use those downloaded inputs for isolated full-size parse/stage/cleanup/load measurements without another provider download. This is a mandatory readiness gate, not an optional smoke; it is not authorized by this proposal. If download limits truncate any feed or the strict parser rejects it, readiness remains unproven. Never derive an IMDb whole-import allowance from only 50,000-row fixtures or an unverified linear extrapolation. The implementation can be delivered disabled with this gate outstanding.

Ordinary scheduled IMDb runs require the same explicit finite whole-invocation byte/disk/time profile as bootstrap; there is no unbounded legacy fallback. Enabling either live mode without that configured, operator-reviewed calibrated profile fails validation. Isolated fake-feed tests and the separately authorized calibration runner use explicit test/calibration allowances and do not certify a live profile. In scheduled mode a cap or transient fault ends the invocation and may retry on the next scheduled day using the configured profile; strict malformed/integrity rejection reports `feed blocked` and suppresses IMDb scheduling for the current process pending remediation/restart. The bootstrap path retains its both-catalog session pause.

Bound cleanup and domain statements using deterministic keys and dependency order. Movies/series/episodes can use stable source keys. Keyset season output groups by `(parent identity, season number)` and aggregate all qualifying episodes for each selected group; do not slice that group's input episodes. This preserves complete season dates and the affected-output cap even when one parent has more seasons than the cap. Limit statement time as well; a very large group can still scan many rows and must be reported/handled within the time bound. Keyset progress is invocation-local; restart replays idempotent upserts. Existing episode staging's `ON CONFLICT DO NOTHING` means replay is duplicate-safe but does not correct changed episode hierarchy; preserve that existing policy in this change and document the limitation, rather than silently expanding refresh semantics.

If measurements show a valid whole-source unit cannot finish within the approved resource allowance, report the specific blocker. Do not implement a new persistent snapshot pipeline or indexes/migrations without an amended scope.

### 5. Preserve artwork behavior; address measured database amplification

IMDb ingestion creates no artwork demand. IGDB admission attaches the imported cover reference using its original fetch time and does not refresh local TTL on replay. Fresh imported covers require no additional IGDB lookup. Missing/expired references are requested only after authorized browse/review selection. Browser rendering continues to use resolved CDN URLs and existing lazy loading; credentials and provider discovery remain server-side.

Measure demand registration at distinct canonical counts 1/25/100 and 1,000 unpaged reviews. If it fails the agreed latency/SQL gate, use a bounded bulk lookup/registration/attachment implementation inside the existing artwork service while preserving every selected review and shared TV identity. Do not silently truncate reviews or add pagination/API changes as a performance fix. Maintenance scans need measured plans too; do not introduce indexes solely from static inspection.

### 6. Observable progress and reproducible readiness gates

Use structured logs/results rather than new persistent status tables. Every session reports a correlation ID, provider/mode, effective non-secret limits, stage, elapsed time, committed pages/rows, admitted/affected rows, remaining-work indicator, cursor bounds, HTTP counts by operation category, retry/conflict counts, and stop reason. Affected upserts are not labeled all-new inserts. Record aggregate provider request metrics separately. Never log credentials, authorization headers, token bodies, raw provider payloads, or signed URLs.

Sanitize retained IMDb error paths too: remove full interpolated SQL/raw-line logging from import/load failures and raw-value parser warnings. Log feed/stage, safe line number or batch range, row count and error category; review exception rendering so it does not reintroduce SQL/provider payloads. Verify these paths with a logger capture fixture, not only successful progress messages.

Implementation starts with an isolated baseline measurement harness using existing test infrastructure. The original app/database are excluded. Use fixture-owned PostgreSQL and fake HTTP; record database/runtime versions, machine/container resources, commit, fixture seed/distribution and command. Match baseline and candidate workloads: one warm-up and five measured runs, cold/warm separately. Report sample count and variance; five run maxima are not a statistically strong tail estimate. Use enough repeated requests within each run for endpoint p50/p95, and retain max transaction duration.

Required matrix:

- Staging sizes 0, 5,000 and 50,000, with 0/1/100% eligible rows, unchanged replay, future releases, updates and a preexisting admission backlog. Increase to 100,000 or a justified representative target only within the agreed local test resource envelope. Small fixtures alone cannot certify a catalog-sized run.
- IMDb episode-heavy parent groups, null/unknown fields, malformed/truncated gzip and middle-batch failure; interruption during each download, staging, cleanup and load phase. These local fixtures establish correctness, not full-feed scale. The separately authorized full-feed calibration above is mandatory for IMDb readiness and must include all three source sizes, peak disk and complete invocation timings.
- Artwork 1/25/100 distinct targets plus 1,000 reviews; cold/fresh/stale states, shared TV identities and repeated pending polling. Run importer and artwork together using deterministic barriers for cover-version and insert races.
- Actual generated SQL plans with `EXPLAIN (ANALYZE, BUFFERS)` for IGDB eligibility, IMDb joins/season aggregates/cleanup, artwork claims/maintenance. Run writes only in disposable rollback/resettable fixtures. Record rows examined/affected, estimates, index use, sort/hash spills, lock waits and WAL where available.
- Per-request SQL count; endpoint latency; stage/admission throughput; transaction p50/p95/max; RSS/allocations/tracked entities; temporary disk high-water; all HTTP attempt timestamps/statuses/concurrency; minimum lease headroom; queue age and retry/conflict counts.
- Faults at page-save/cursor-commit boundaries, crash after cursor commit, admission-only retry, non-retryable failing admission prefix, A/B lease takeover, immediate post-crash lease-busy launch, blocked transaction crossing expiry, token failure classes, 401 refresh, 429 cooldown, delayed response bodies and stop during limiter waits. Include completed-scan/pending-admission relaunch and a large first bounded scheduled incremental window.

Readiness requires all configured caps to hold, no new send/work unit after stop, no lost committed progress or duplicate identities, fresh imported cover reuse, no mass artwork demand, bounded memory/disk, and measurable progress for both healthy import and artwork under mixed load. Provisionally require lease duration at least twice the measured maximum renewal interval including operation/commit reserve, with positive ownership headroom for successful commits; deliberately forced expiry/takeover must instead reject stale commits and recover safely. Before measuring, record local resource and endpoint latency ceilings; compare both absolute targets and baseline regression. Explain any SQL/lock-growth trend rather than labeling it acceptable because tests pass.

Data and performance specialists review this design before implementation and review actual measurements before any larger provider run. Prior five-game smoke and static reviews are useful context, not bulk readiness evidence. The independent new-change plan/implementation review checkpoints are separate from the accepted cover-art waiver.

Pre-implementation review evidence is recorded in the orchestration run log: native data and performance planning reviews, parent reconciliation, and the independent plan review using both specialist profiles. Task 1.1 must check this recorded disposition and any outstanding review limitation before implementation starts. Subsequent measured-result review is a distinct gate.

## Risks / Trade-offs

- No durable session counters → crash restarts need a new deliberate allowance; automated restart profiles are excluded.
- IMDb full replay consumes bandwidth/time → select a sufficient measured whole-import allowance; durable snapshot recovery is deferred.
- New bounds can leave partial domain progress → report stage/backlog precisely and test idempotent replay; do not claim catalog atomicity.
- Shared provider limiter is process-local → one app process during bootstrap; do not start a second importer alongside the original app.
- Larger catalogs magnify synchronous demand work and maintenance → baseline SQL/latency/plan measurements precede targeted fixes and larger profiles.
- No schema changes in scope → a demonstrated indexing/persistence need becomes a concrete amendment, not an implicit addition.

## Migration Plan

No database migration or reset is proposed. First approve/apply the implementation with provider and bootstrap flags disabled. Validate focused fake-provider tests and isolated PostgreSQL measurements, reconcile reviews, and document the measured profile. Live calibration requires a separately explicit bounded request allowance and a verified target/process plan; the full-feed IMDb calibration is required before IMDb readiness, while an IGDB sample is selected only if needed. This proposal starts neither. A larger import requires the reviewed measurement results and its own finite approved profile.

Rollback disables the new bootstrap mode and restores the prior application revision while retaining idempotently committed catalog/staging progress. Existing schema is unchanged. Do not reset the database, erase cursors or rewind provider watermarks to roll back the orchestration.

## Open Questions

- Final throughput targets, IMDb byte/disk/deadline allowance, endpoint latency ceilings and larger IGDB budgets will be set from representative local measurements before live activation. No throughput estimate is yet supported.

## Provider references

Checked 2026-09-21: [IGDB documentation](https://api-docs.igdb.com/) documents up to 4 requests/second, 8 open requests, and 500 items/request; these are provider ceilings, not recommended bootstrap settings. [IMDb noncommercial dataset documentation](https://www.imdb.com/interfaces/?mode=desktop) describes daily refreshed gzip TSV datasets. Only documentation was fetched; no catalog API or dataset download was performed.
