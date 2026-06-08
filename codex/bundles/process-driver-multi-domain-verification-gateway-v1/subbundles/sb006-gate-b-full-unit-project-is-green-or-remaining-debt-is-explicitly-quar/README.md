# SB006 — Gate B: full unit project is green or remaining debt is explicitly quarantined with owner and reopen trigger.

## Status
- Status: `Completed`

## Objective
Gate B: full unit project is green or remaining debt is explicitly quarantined with owner and reopen trigger.

## Covered Inputs
- Raw user request in bundle://inputs/raw-request.md.
- Current-state analysis in bundle://analysis/01-current-state-review.md.
- Normalized requirements in bundle://requirements/01-normalized-requirements.md.

## Prerequisites
- Complete all prior subbundles in plan order.
- If this is a phase gate, all phase-owned source scans and focused tests must pass before downstream work continues.

## Exact Source References
- repo://src/CanDoItAll.Processes.Core
- repo://src/CanDoItAll.Processes.Drivers.Abstractions
- repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification
- repo://src/CanDoItAll.Processes.Drivers.RuntimeEvidence
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch
- repo://tests/CanDoItAll.Tests.Unit
- repo://tests/CanDoItAll.Tests.Integration

## Deliverables
- Source changes matching the objective, or documentation/test changes when explicitly scoped.
- Updated tests and architecture guards.
- Updated proof artifacts under `proof/SB006/` if this is a critical gate.

## Dependency Impact
- Downstream phases assume this subbundle preserved read-only driver semantics, Core dependency cleanliness, and no runtime host/registry/selector behavior.

## Validation Depth
- Build and relevant focused tests.
- Source scans for forbidden runtime/Core/driver/UI/stub drift.
- Architecture tests for package/dependency boundaries.
- Semantic Adequacy Gate with manifest, changed-file hashes, source assertions, adversarial negative proof, semantic positive proof and anti-stub audit.

## Implementation Steps
1. Re-read current source references before editing.
2. Implement only the named coherent slice; do not opportunistically add runtime host behavior.
3. Update or add tests first when behavior is new.
4. Run focused proof before moving to the next subbundle.
5. Update reviews/01-execution-report.md row for this subbundle.

## Scope Exceptions
- Runtime host/registry/selector/DI/manager/scheduler/workflow integration remains out of scope.
- Execution-capable drivers remain out of scope.
- UI/browser/mobile proof remains out of scope unless UI/media drift occurs, which should fail the bundle.

## Do Not Do
- Do not add generic runtime driver discovery.
- Do not register drivers in DI.
- Do not add manager commands.
- Do not read arbitrary files or call external systems.
- Do not mutate process state, claims, transitions, finalizers, retries, workspace, or storage.
- Do not add UI/mobile screenshots.

## Acceptance Checklist
- [x] Objective implemented.
- [x] Tests added/updated.
- [x] No forbidden dependency/runtime tokens.
- [x] No UI/media drift.
- [x] Existing behavior preserved.
- [x] Critical proof manifest and semantic invariants complete.

## Proof Required
- Build/focused test transcript.
- Source scan transcript.
- Anti-stub audit.
- proof/SB006/manifest.md and proof/SB006/semantic-invariants.md.

## Closure Proof
- Solution build passed with 0 warnings and 0 errors: `bundle://proof/SB006/transcripts/gate-b-solution-build-no-restore.txt`.
- Full unit project passed with 1058 passed, 21 SB004-owned skips, and 0 failures: `bundle://proof/SB006/transcripts/gate-b-full-unit-tests.txt`.
- Remaining debt ledger owns every skip and has a reopen trigger: `bundle://proof/SB006/remaining-debt-ledger.md`.
- Semantic positive proof and adversarial negative proof passed: `bundle://proof/SB006/transcripts/gate-b-proof-index.txt`; `bundle://proof/SB006/transcripts/red-team-status-only-gate-b-rejection.txt`.
- Proof manifest and semantic invariants: `bundle://proof/SB006/manifest.md`; `bundle://proof/SB006/semantic-invariants.md`.

## Browser Validation Logging
- N/A — runtime/service/Core/driver work only. If UI/media files change unexpectedly, fail and re-scope.

## Progression Gate
- Critical foundation: downstream phases must not start until this gate passes all semantic adequacy checks.

## Suggested Agent Prompt
Implement SB006: Gate B: full unit project is green or remaining debt is explicitly quarantined with owner and reopen trigger.. Preserve all hard constraints from bundle://requirements/02-hard-constraints.md and close proof in reviews/01-execution-report.md.
