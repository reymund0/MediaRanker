# Bulk catalog bootstrap checkpoint

Date: 2026-09-23 (America/Los_Angeles)
Branch: `codex/bulk-catalog-bootstrap`
Pre-implementation baseline: `346ccc6c8e446b69cd235de3ef9e5a87904e63c6`

This is a user-requested checkpoint for a temporary pivot. The implementation remains disabled, with **18 of 22 OpenSpec tasks complete**. This commit does not waive outstanding review/measurement gates or authorize live imports, deployment, a PR, or archival.

## Implemented and verified

- Finite, explicit per-launch bootstrap selection and cumulative budgets; ordinary catalog scheduling respects bootstrap outcomes.
- IGDB bounded admission, backpressure, lease fencing and fresh-context recovery. Scheduled invocations now span fresh scopes under one cumulative session and total page cap. Failed retries clear stale tracking, including final-page completion.
- IMDb strict complete-feed gating before cleanup/load, bounded parsing and statements, batch allocation limits, sanitized errors, strict UTF-8/BOM handling, and cleanup that preserves an existing failure.
- Existing demand-driven artwork behavior preserved; legacy manual IMDb triggers return 410.
- 152 focused unit tests passed. PostgreSQL/artwork checks initially passed 58/59; the remaining failure was a fake client bypassing budget accounting. After correcting the fake, all 20 recovery cases passed; the other 39 cases had already passed.
- The opt-in synthetic parser benchmark passed: approximately 1/10 MiB TSV targets took 674.0/4,930.2 ms, about 1.47/2.02 compressed MiB/s for these inputs. One sample each; no full-feed forecast.
- Strict OpenSpec validation and whitespace checks passed. Provider defaults, dependencies, migrations and accepted automatic-cover-art artifacts were unchanged.

## Resume here

1. Read `openspec/changes/bulk-catalog-bootstrap/verification.md`, then the change's proposal, design, spec and tasks. Run:
   ```powershell
   $env:OPENSPEC_TELEMETRY='0'
   openspec instructions apply --change bulk-catalog-bootstrap --json
   openspec validate bulk-catalog-bootstrap --strict --no-interactive
   ```
2. Resolve the four open tasks without silently narrowing their requirements:
   - **1.3:** A matched checkpoint-baseline importer/artwork mixed workload is missing. The frozen baseline supplement exercised only fake client traffic, not the candidate's actual mixed workload.
   - **5.3:** Reconcile remaining comparison/variance limits. Original 5k update samples near 18 seconds were not reproduced by a fresh repeat (median 1.595 seconds). Retain both; do not invent a cause. Full 50k admission and backlog showed overhead, not a speedup.
   - **6.2:** Independent coverage is incomplete. Opus found no major/blocking code defects and confirmed the repairs, but five selected test/harness files were filtered from its packet. Native review covered them; that does not turn the external verdict into a pass.
   - **6.3:** Tested operating procedures are documented, but a measured live operating profile/forecast is absent. The task was deliberately unchecked again.
3. Obtain direction before accepting exceptions or commissioning another independent review. The single automatic Claude recheck has been consumed. **Future Opus 5.5 calls in this conversation must use fixed medium effort.** The completed review started on high before that preference changed; do not restart it or change persistent model settings.
4. Keep full-feed IMDb readiness separate. The verification document contains the proposed isolated target and exact three-download, byte, disk, memory and time envelope. That calibration has not been authorized or run. Do not enable providers or use the original application's database.

## Remaining risks and evidence limits

- The one-byte gzip boundary reader is correct on tested integrity cases but needs real-feed CPU/throughput calibration.
- Heavy IMDb season joins discarded 77.11 million candidate rows in the diagnostic fixture; full-scale statement costs remain unknown.
- The IGDB remaining-count query can scale poorly and deliberately stops with admission-blocked if it fails. Actual 50k page/count plans took roughly 73–82 ms; larger-scale behavior is unproven.
- Forced temporary-file deletion failures have not been exercised in tests.
- Recorded fixture working sets reached about 231 MiB. Temporary-disk high-water was unavailable in admission/artwork fixtures; neither result certifies full-feed resource use.

## Evidence locations

The committed `openspec/changes/bulk-catalog-bootstrap/verification.md` contains the portable results and limitations. Detailed local evidence below is ignored by Git and may be absent in another checkout:

- `.clanker/2026-09-21-orchestration-nation.md` — append-only run `bootstrap-1`.
- `.clanker/resume-checkpoint.md` — local coordination state.
- `.clanker/test-results/bootstrap-review-fixes-units.trx`
- `.clanker/test-results/bootstrap-review-fixes-postgres.trx` — retains the initial harness failure.
- `.clanker/test-results/bootstrap-review-fixes-recovery-budgeted.trx`
- `.clanker/test-results/bootstrap-parser-throughput.trx`
- `.clanker/measurements/` — baseline/candidate matrices and actual plan diagnostics.
- `.clanker/baseline-bin-346ccc6/` — frozen baseline assemblies; preserve them.
- `.clanker/reviews/bulk-catalog-bootstrap-implementation-20260922-1/implementation-3/report.json` — completed, verdict `incomplete`; source fingerprint matched before administrative task updates.

No workers, tests or reviews remain active. Docker was started under prior approval and may still be running. The original app/database were untouched. On resume, use the orchestration skill's write-reservation checks before edits and keep the parent as sole writer of shared OpenSpec state and the run log.
