# SB21 - Route Order And Exception Safety Review

## Status

- Completed
## Objective

Red-team DispatchAsync route order, claim lease safety, exception handling, and terminal run checks after subprocess extraction.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- Current branch source under `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/`.

## Prerequisites

- Previous subbundle gate passed.
- No uncommitted unrelated changes.
- For production movement subbundles, source inventory from SB02 must be current.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.FinalizerContextFactory.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePlanner.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Source changes and/or proof artifacts matching this subbundle objective.
- Manifest under `proof/SB21/manifest.md`.
- Semantic invariants under `proof/SB21/semantic-invariants.md`.
- Command transcripts under `proof/SB21/transcripts/`.

## Dependency Impact

- Feeds SB22. If this subbundle changes route/projection semantics, reopen the previous gate.

## Validation Depth

- Focused source scan.
- Focused tests where behavior moved.
- Full/module build at critical gates.
- Line-count review at refactor gates.
- No-core/no-driver/no-UI/no-prohibited-viewport scan.

## Implementation Steps

1. Re-read the exact source references.
2. Apply the smallest complete source movement for this subbundle.
3. Preserve existing wrappers where they protect test compatibility.
4. Add focused tests or source assertions before claiming completion.
5. Record proof artifacts.
6. Update the execution report.

## Scope Exceptions

- Process Core extraction is out of scope.
- Production process driver APIs are out of scope.
- UI/browser work is out of scope unless an unexpected source change makes it necessary.

## Do Not Do

- Do not create `CanDoItAll.Processes.Core`.
- Do not create process driver APIs, driver packs, registries, or descriptors.
- Do not move EF entities or storage implementations.
- Do not hide side effects in pure helpers.
- Do not change subprocess artifact key/lineage formats without explicit parity proof.
- Do not run small/medium/mobile proof.

## Acceptance Checklist

- [x] Objective satisfied.
- [x] Existing behavior preserved.
- [x] Tests/source scans recorded.
- [x] No Process Core or production driver API introduced.
- [x] No UI or prohibited viewport proof drift.
- [x] Execution report updated.

## Proof Required

- Manifest.
- Semantic invariants.
- Source scan.
- Focused tests when behavior moved.
- Build at critical gates.
- Anti-stub audit.

## Browser Validation Logging

- N/A expected. Runtime/service refactor only. If UI unexpectedly changes, record large desktop/PC proof only and explain why.

## Progression Gate

- Proceed only if acceptance checklist and proof are complete.

## Suggested Agent Prompt

Implement SB21 only. Keep changes module-local. Preserve subprocess dispatch/projection behavior. Do not start Process Core or production driver APIs. Record proof before moving to the next subbundle.
