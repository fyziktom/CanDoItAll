# SB006 - Gate B - subprocess lifecycle parity

## Status
Prepared.

## Objective
Prove subprocess lifecycle rule extraction did not change behavior.

## Covered Inputs
- User requested fewer, broader, meaningful phases that move toward Process Core and future drivers safely.
- Current branch has a narrow Core seed with route rules only.

## Prerequisites
- Previous subbundle gate is closed.
- Active branch is `maf-processes-refactor`.
- Core seed from `process-core-narrow-seed-route-rules-driver-proposal-prep-v1` is present and green.

## Exact Source References
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables
- Critical manifest
- Semantic invariants
- Parity transcripts

## Dependency Impact
- Downstream phases depend on this subbundle preserving pure/application boundary discipline.
- If this subbundle moves side-effectful behavior into Core, all downstream Core proof is invalid.

## Validation Depth
- Build
- Core dependency scan
- Subprocess lifecycle unit tests
- Focused subprocess integration tests

## Implementation Steps
1. Re-open the exact source references above and verify current shape.
2. Make the smallest complete production move or documentation/test-only update required by the objective.
3. Add or update focused tests before relying on manual proof.
4. Run the subbundle proof commands and store transcripts under `proof/SB006/transcripts/`.
5. Record semantic invariants and reopen triggers before moving to the next subbundle.

## Scope Exceptions
- Broad Process Core extraction is out of scope.
- Production driver APIs are out of scope.
- UI/browser/mobile validation is out of scope unless production UI files unexpectedly change, in which case the change should be reverted.

## Do Not Do
- Do not move EF, workspace, storage, filesystem, claim lifecycle, transition execution, AgentFramework execution, finalizer application, process state mutation, or runtime driver dispatch into Core.
- Do not add production process-driver APIs, registries, DI registrations, manager commands, or runtime selectors.
- Do not create UI/browser/mobile/small/medium proof artifacts for this runtime/service refactor.
- Do not collapse execution report rows.

## Acceptance Checklist
- [ ] Objective implemented.
- [ ] Existing behavior preserved.
- [ ] No forbidden Core dependency.
- [ ] No production driver API.
- [ ] Tests/scans recorded.
- [ ] Execution report has an individual `SB006` row.

## Proof Required
- Build or documented deferral if docs-only and covered by next critical gate.
- Focused unit/integration parity where production behavior moved.
- Source scans for Core forbidden dependencies and driver tokens.
- No UI/media drift scan.
- Anti-stub scan.

## Browser Validation Logging
N/A - runtime/service/backend architecture refactor only. Do not create small/medium/mobile/browser screenshots.

## Progression Gate
Do not start the next subbundle if this subbundle changed Core or module behavior without passing focused proof and source scans.

## Suggested Agent Prompt
Implement `SB006 - Gate B - subprocess lifecycle parity` exactly as scoped. Preserve existing process behavior, keep Core pure, keep driver work docs/tests-only, and record proof before proceeding.
