# SB049 - Red-team report-only, happy-path-only, and status-only traps

## Status
- Completed
## Objective
Advance final red-team, fake-proof resistance, and completed validator by completing: Red-team report-only, happy-path-only, and status-only traps.

## Covered Inputs
- `inputs/raw-request.md`
- `inputs/source-artifacts.md`
- `analysis/01-current-state.md`

## Prerequisites
- Previous subbundle closure gate passed.
- For critical gates, all upstream proof manifests and semantic invariants must be present.

## Exact Source References
- repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDomainEvidenceReadOnlyAdapters.cs
- repo://src/CanDoItAll.Processes.Drivers.Abstractions
- repo://src/CanDoItAll.Processes.Core
- repo://tests/CanDoItAll.Tests.Unit
- repo://tests/CanDoItAll.Tests.Integration

## Scope
- Keep the change read-only and deterministic. Preserve current behavior while improving structure, tests, or docs.

## Dependency Impact
- This subbundle is part of P17 - Final red-team, fake-proof resistance, and completed validator. Downstream work is invalid if its closure proof is weak or report-only.

## Validation Depth
- Focused tests plus nearest critical gate coverage. Include local source assertions and no-side-effect scans.

## Implementation Steps
1. Re-read the exact source references.
2. Implement only the scoped change.
3. Add or update focused tests before broad tests.
4. Run source scans for forbidden runtime, DI, file/network, storage/workspace, mutation, Core reverse dependency, UI/media drift, and stubs.
5. Record command transcripts and changed-file hashes.

## Scope Exceptions
No runtime host, registry, selector, DI registration, manager command, scheduler/workflow hook, shell execution, Graph/Office runtime call, file/network access, workspace/storage write, process mutation, claim/transition/finalizer/retry mutation, or UI work is approved.

## Do Not Do
- Do not add generic `Verify(object)` or lane-based object dispatch.
- Do not add service registration or manager commands.
- Do not create execution-capable drivers.
- Do not silently skip tests or collapse report rows.

## Acceptance Checklist
- [x] Build passes with zero warnings/errors.
- [x] Full unit suite passes or any skip/debt is explicitly owned and justified.
- [x] Focused tests for this subbundle pass.
- [x] Source scans pass.
- [x] No UI/media drift.
- [x] Critical proof manifest exists when required.

## Proof Required
- Record source assertions and focused transcript paths; nearest critical gate may carry full manifest closure.

## Closure Proof
- Final red-team review: `bundle://reviews/02-final-red-team-review.md`.
- Red-team trap scan proof: `bundle://proof/SB051/transcripts/p17-red-team-trap-scans.txt`.
- Completed validator preflight proof: `bundle://proof/SB051/transcripts/completed-validator-preflight-expected-pending.txt`.
- Critical P17 proof manifest: `bundle://proof/SB051/manifest.md`.

## Browser Validation Logging
- N/A runtime/service/Core/driver work. If UI/media files change unexpectedly, fail and re-scope.

## Progression Gate
- Proceed only if local proof is sufficient and no downstream dependency is weakened.

## Suggested Agent Prompt
Implement SB049 carefully using source-backed proof. Do not trust existing reports without opening current branch code.

