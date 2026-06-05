# SB09 - Capability Gap Facts And Inspector

## Status

Prepared.

## Objective

Extract active child-step query/facts and capability-gap block reason building into a focused helper.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- Current branch source under `src/CanDoItAll.Modules.Processes/Automation/Dispatch/`.

## Prerequisites

- Previous subbundle gate passed.
- No uncommitted unrelated changes.
- For production movement subbundles, source inventory from SB02 must be current.

## Exact Source References

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.FinalizerContextFactory.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePlanner.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables

- Source changes and/or proof artifacts matching this subbundle objective.
- Manifest under `proof/SB09/manifest.md`.
- Semantic invariants under `proof/SB09/semantic-invariants.md`.
- Command transcripts under `proof/SB09/transcripts/`.

## Dependency Impact

Feeds SB10. If this subbundle changes route/projection semantics, reopen the previous gate.

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

- [ ] Objective satisfied.
- [ ] Existing behavior preserved.
- [ ] Tests/source scans recorded.
- [ ] No Process Core or production driver API introduced.
- [ ] No UI or prohibited viewport proof drift.
- [ ] Execution report updated.

## Proof Required

- Manifest.
- Semantic invariants.
- Source scan.
- Focused tests when behavior moved.
- Build at critical gates.
- Anti-stub audit.

## Browser Validation Logging

N/A expected. Runtime/service refactor only. If UI unexpectedly changes, record large desktop/PC proof only and explain why.

## Progression Gate

Proceed only if acceptance checklist and proof are complete.

## Suggested Agent Prompt

Implement SB09 only. Keep changes module-local. Preserve subprocess dispatch/projection behavior. Do not start Process Core or production driver APIs. Record proof before moving to the next subbundle.
