# SB07: Manager Chat Resolution And Run Inspection Hardening

## Status

- Status: Completed

## Objective

- Harden selected-run manager chat resolution and inspection context.

## Covered Inputs

- RN07 maps to RQ07.
- Preserve the original bundle scope for this subbundle.

## Prerequisites

- SB03 completed.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerAgentResolver.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.ManagerChat.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessObservationIntentResolverTests.cs

## Deliverables

- Shared resolver with reason codes, confidence, and manager chat context summary.

## Dependency Impact

- SB10, SB13, and SB18 depend on trustworthy manager resolution.

## Validation Depth

- Critical foundation with adversarial ambiguity proof.
- Include failing-first or adversarial proof when behavior changes, passing proof, source assertions, anti-stub audit, changed-file hashes, classification, and proof-debt closure status.

## Implementation Steps

- Inspect the referenced source and nearby tests.
- Implement the smallest correct change set for this subbundle only.
- Update proof artifacts under bundle://proof/SB07/.

## Do Not Do

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths.
- Do not use docs-only changes to satisfy runtime proof requirements.

## Acceptance Checklist

- Deliverables are complete.
- Required tests or source assertions are recorded.
- Execution report gate rows are updated.

## Closure Evidence

- Runtime resolver: repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerAgentResolver.cs
- Manager chat service consumer: repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs
- Selected-run component consumer: repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.ManagerChat.cs
- Tests: repo://tests/CanDoItAll.Tests.Integration/ProcessObservationIntentResolverTests.cs
- Manifest: bundle://proof/SB07/manifest.md
- Semantic invariants: bundle://proof/SB07/semantic-invariants.md
- Passing proof: bundle://proof/SB07/transcripts/passing.txt
- Adversarial proof: bundle://proof/SB07/transcripts/failing-first.txt
- Browser validation: N/A; no manager chat markup, CSS, route, layout, or visible UI rendering component changed.

## Proof Required

- bundle://proof/SB07/manifest.md
- bundle://proof/SB07/semantic-invariants.md when the subbundle is critical or behavior-changing.
- Command transcripts under bundle://proof/SB07/transcripts/.

## Browser Validation Logging

- Required if manager chat UI changes; capture open chat/readability proof.
- Update bundle://reviews/01-execution-report.md if browser proof is applicable.

## Progression Gate

- SB10 and SB13 may start only after manager ambiguity proof passes.

## Suggested Agent Prompt

- Execute SB07 literally, preserve runtime genericity, and close owned proof before moving downstream.
