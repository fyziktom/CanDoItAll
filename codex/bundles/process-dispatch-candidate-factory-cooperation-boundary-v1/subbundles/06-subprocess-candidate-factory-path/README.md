# SB06 - Subprocess candidate factory path

## Status

Prepared.

## Objective

Move subprocess DispatchCandidate construction behind factory with exact parity tests.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-source-artifacts.md`
- `analysis/01-current-state.md`

## Prerequisites

All previous subbundles through SB05 closed.

## Exact Source References

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Cooperation.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationLoader.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchTechnicalAgentBindingCoordinator.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Source changes or proof artifacts required by this subbundle.
- Updated proof manifest under `proof/SB06/manifest.md`.
- Semantic invariants under `proof/SB06/semantic-invariants.md`.
- Command transcripts under `proof/SB06/transcripts/`.

## Dependency Impact

This subbundle is part of a staged candidate-construction refactor. Downstream subbundles must not proceed if candidate field parity or side-effect boundaries are uncertain.

## Validation Depth

- Source scan where applicable.
- Focused unit or integration tests where behavior changes.
- Build or targeted build for any production movement.
- No UI/browser validation unless scope breach.

## Implementation Steps

1. Re-read the relevant source files before editing.
2. Update the field map and traceability if this subbundle moves candidate-related behavior.
3. Make the smallest production movement that satisfies the objective.
4. Preserve wrappers where they protect existing test coverage.
5. Record source hashes and command transcripts.
6. Run the progression gate before starting the next subbundle.

## Scope Exceptions

- Process Core extraction is explicitly out of scope.
- Production driver APIs are explicitly out of scope.

## Do Not Do

- Do not create `CanDoItAll.Processes.Core`.
- Do not create `IProcessDriverPack`, driver registry, or driver pack projects.
- Do not move EF writes or execution side effects into pure helpers.
- Do not rename process tools or public APIs.
- Do not create small/medium/mobile proof artifacts.

## Acceptance Checklist

- [ ] Objective implemented.
- [ ] Candidate behavior parity maintained.
- [ ] No forbidden projects/APIs introduced.
- [ ] Tests/source scans recorded.
- [ ] Browser validation logged as N/A or scope breach recorded.

## Proof Required

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- targeted tests/source scans
- anti-stub scan for new helper files

## Browser Validation Logging

N/A expected. Runtime/service refactor only. Do not run small/medium/mobile proof.

## Progression Gate

Do not continue until this subbundle's tests/source scans are passing and the proof manifest is complete.

## Suggested Agent Prompt

Implement SB06: Subprocess candidate factory path. Follow the hard constraints from `shared-prompts/implementation-prompt.md` and do not proceed to the next subbundle until the progression gate is satisfied.
