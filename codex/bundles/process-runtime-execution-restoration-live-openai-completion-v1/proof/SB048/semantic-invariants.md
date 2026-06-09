# SB048 Semantic Invariants

## Status
Completed.

## Invariant SB046_INV_001
- Invariant ID: `SB046_INV_001`
- Source raw note: failed process runs need real runtime restoration and triage, not report-only failure prose.
- Expected behavior: failed or blocked process execution is classified with typed categories and recovery modes/actions.
- Disallowed shallow implementation: logging error strings without `AgentFailureCategory`, `ProcessStepBlockReasonCode`, `ProcessStepRecoveryOption`, persisted blocked state, or recovery routing.
- Passing proof: `bundle://proof/SB046/structured-failure-taxonomy-proof.md` and `bundle://proof/SB048/transcripts/failure-triage-observability-tests.txt`.

## Invariant SB047_INV_001
- Invariant ID: `SB047_INV_001`
- Source raw note: operators need troubleshooting readback for blocked/failed process runs.
- Expected behavior: API/run-detail and operator read models expose block reason, next recovery action, recovery options, run-health recommendation, invariant diagnostic recommended actions, outbox health, escalations, and attempt timeline.
- Disallowed shallow implementation: exposing only a generic failed status or internal diagnostic string.
- Passing proof: `bundle://proof/SB047/operator-troubleshooting-readback-proof.md` and `bundle://proof/SB048/transcripts/failure-triage-observability-tests.txt`.

## Invariant SB048_INV_001
- Invariant ID: `SB048_INV_001`
- Source raw note: Gate P must prove failure triage and observability before release-candidate smoke.
- Expected behavior: focused failure taxonomy/readback tests pass, typed source assertions exist, active bundle-path scan is clean, and no forbidden runtime driver host or driver mutation surface is present.
- Disallowed shallow implementation: claiming observability from docs, old UI screenshots, or happy-path launch tests without failure/readback coverage.
- Failing-first/negative proof: `bundle://proof/SB048/red-team/failure-triage-shallow-proof-rejected.md`
- Passing proof: `bundle://proof/SB048/transcripts/failure-triage-observability-tests.txt`

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Agent failure category and recovery decision | Dispatch recovery packet builder and recovery decision factory | Rework packet creation, recovery ledger, recovery context | Failed automation detail is classified into typed category/mode and reused for targeted repair or escalation | Rejects log-string-only failure proof |
| Blocked-step recovery state | `ProcessStepRunBlockState` and `ProcessRecoveryRouter` | Runtime step readback, API run detail, operator UI/read model | Blocked transition stores reason code, recovery options, next action, classification, and evidence fingerprint | Rejects generic failed status without typed recovery action |
| Run and step health view models | Runtime read model/loaders | API clients and Process Workspace operators | Health summary exposes recovery classification, actionable reason, recommended action, missing-artifact count, outbox health, and attempts | Rejects status-only readback |
| Runtime invariant diagnostics | Runtime invariant auditor and process service diagnostics | Operator troubleshooting read model | Weak artifacts, duplicate lineage, blocked recovery state, and manual transition failures surface with recommended actions and evidence keys | Rejects hidden diagnostics that are not operator-readable |
| Dead-lettered outbox health | Process outbox records/read model | Operator health, escalations, attempt timeline | Exhausted automation dispatch projects as dead-letter health with escalation and timeline entries | Rejects outbox-only proof without health projection |

## Shallow-Pass Trap
A fake Gate P closure could show a failed process row, a log message, or a previous UI screenshot and claim failure observability. SB048 rejects that by requiring typed taxonomy source assertions, API/read-model tests, invariant diagnostic readback, outbox health projection, and clean forbidden-surface scans.

## Semantic Positive Proof
- `bundle://proof/SB046/structured-failure-taxonomy-proof.md`
- `bundle://proof/SB047/operator-troubleshooting-readback-proof.md`
- `bundle://proof/SB048/transcripts/source-assertions.txt`
- `bundle://proof/SB048/transcripts/failure-triage-observability-tests.txt`

## Adversarial Negative Proof
- `bundle://proof/SB048/red-team/failure-triage-shallow-proof-rejected.md`

## Anti-Stub Audit
- `bundle://proof/SB048/transcripts/no-transient-bundle-path-scan.txt`
- `bundle://proof/SB048/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- `bundle://proof/SB048/transcripts/production-driver-runtime-host-scan.txt`
- No active bundle paths or forbidden production process driver runtime host surfaces were found.
