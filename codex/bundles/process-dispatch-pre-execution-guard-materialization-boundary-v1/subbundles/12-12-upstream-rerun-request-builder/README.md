# SB12 - Upstream rerun request builder

## Status

Prepared.

## Objective

Upstream rerun request builder and directive parity.

## Covered Inputs

- User request to continue small dispatcher isolation steps.
- Current branch state after candidate factory/cooperation extraction.
- No Process Core / no production driver API constraint.

## Prerequisites

- Previous subbundle completed: SB11.
- Current branch builds or the failure is recorded as an entry blocker.
- For critical gates: all earlier source movement must have proof.

## Exact Source References


- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationLoader.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- new/updated module-local helpers under `src/CanDoItAll.Modules.Processes/Automation/Dispatch/`


## Deliverables

Upstream rerun request builder and directive parity.

## Dependency Impact

This subbundle unlocks: SB13.

If this subbundle is wrong, downstream proof may not be trustworthy because the dispatch pre-execution route can block, rerun, or continue incorrectly.

## Validation Depth

Focused unit/integration tests + source scans.

## Implementation Steps

1. Re-read current source before editing.
2. Implement only the scoped movement.
3. Preserve wrappers when possible.
4. Add or update focused tests before relying on source movement.
5. Record command transcripts and source assertions.
6. Update `reviews/01-execution-report.md`.
7. Stop if any critical gate fails.

## Scope Exceptions

Process Core and production driver APIs are explicitly out of scope.

## Do Not Do


- Do not create Process Core.
- Do not create production driver APIs.
- Do not touch UI.
- Do not run small/medium/mobile proof.
- Do not hide side effects inside pure helpers.
- Do not rename existing process runtime behavior without explicit tests.


## Acceptance Checklist

- [ ] Scope limited to this subbundle.
- [ ] Behavior parity proven.
- [ ] No Process Core.
- [ ] No production driver API.
- [ ] No UI changes.
- [ ] No prohibited proof paths.
- [ ] Evidence recorded.

## Proof Required

- build/test transcript,
- source assertion,
- anti-stub scan,
- no-core/no-driver scan,
- no prohibited viewport proof scan,
- line-count or downstream check when relevant.

## Browser Validation Logging

N/A expected. Runtime/service refactor only. Do not run small/medium/mobile proof.

## Progression Gate

May continue only if scoped proof passes.

## Suggested Agent Prompt

Implement SB12 from `process-dispatch-pre-execution-guard-materialization-boundary-v1`. Preserve behavior, record proof, and do not broaden scope.
