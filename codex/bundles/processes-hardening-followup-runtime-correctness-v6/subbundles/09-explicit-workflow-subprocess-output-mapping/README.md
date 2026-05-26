# SB09: Replace heuristic workflow/subprocess artifact matching with explicit mapping.

## Objective

Replace heuristic workflow/subprocess artifact matching with explicit mapping.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Add process definition mapping from workflow artifact output id/name/kind to process artifact expectation id.
- Add subprocess parent mapping from child process artifact expectation id to parent expectation id.
- Block ambiguous mapping instead of guessing by kind/title/summary.
- Keep legacy heuristic as warning-only compatibility fallback.
- Add tests with multiple same-kind artifacts where heuristic would choose the wrong artifact.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.

## Status

- Completed

## Covered Inputs

- RN07 route workflow/subprocess artifacts heuristically instead of explicitly.
- RQ07 workflow/subprocess output mapping.

## Prerequisites

- SB08 closure gate passes.
- Artifact validation and identity foundations remain trusted.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs
- repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessWorkflowExecutorIntegrationTests.cs

## Deliverables

- Explicit workflow output id/name/kind to process artifact expectation mapping.
- Explicit child subprocess expectation to parent expectation mapping.
- Ambiguous mapping blocks instead of guessing.
- Legacy heuristic retained only as warning compatibility fallback.

## Dependency Impact

- SB11 extracts workflow/subprocess mapper after behavior lands.
- SB14 red-team closure depends on ambiguous same-kind artifact rejection.

## Validation Depth

- Tests with multiple same-kind artifacts where heuristic selection would be wrong.
- Positive tests with explicit mappings.
- Source assertion for block-on-ambiguous behavior.

## Implementation Steps

- Add strongly typed mapping model or parser to process definitions.
- Build workflow output mapping index from explicit configuration.
- Build subprocess parent mapping from explicit child expectation ids.
- Emit compatibility warning rather than silently guessing when legacy fallback is used.
- Record proof under `bundle://proof/SB09/`.

## Do Not Do

- Do not satisfy expectations by title/summary fragments when explicit mapping is absent and ambiguous.
- Do not make mappings software-specific.
- Do not weaken content validation while changing projection routing.

## Acceptance Checklist

- Ambiguous same-kind outputs block with actionable diagnostics.
- Explicit workflow and subprocess mappings satisfy the intended expectation.
- Legacy fallback is warning-only and covered by tests.
- Focused tests pass.

## Proof Required

- `bundle://proof/SB09/manifest.md`
- `bundle://proof/SB09/semantic-invariants.md`
- Failing-first or red-team transcript.
- Passing focused test transcript.
- Changed-file SHA-256 transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A: SB09 changes runtime projection mapping only.

## Progression Gate

- Closed. SB10/SB11 may proceed because explicit workflow/subprocess mapping blocks ambiguous heuristic matches.

## Completion Notes

- Artifact expectations now persist explicit workflow output id/name/kind fields and explicit subprocess child expectation mapping.
- Workflow and subprocess projectors resolve explicit mappings before compatibility fallback.
- Ambiguous same-kind workflow/subprocess outputs block instead of guessing.
- Single-candidate legacy fallback remains warning-only.
- PostgreSQL migration `20260526013931_ProcessArtifactExplicitOutputMappings` was added; no SQLite runtime or migration dependency was introduced.

## Suggested Agent Prompt

- Implement SB09 explicit output mapping, update `proof/SB09`, run focused workflow/subprocess tests, and record gate closure.
