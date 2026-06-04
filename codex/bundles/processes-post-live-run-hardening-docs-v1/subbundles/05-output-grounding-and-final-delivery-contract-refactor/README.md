# SB05: Output Grounding And Final Delivery Contract Refactor

## Status

- Status: Completed

## Objective

- Refactor project-structure output grounding and final external delivery proof into a dedicated generic service.

## Covered Inputs

- RN05 maps to RQ05.
- Preserve the original bundle scope for this subbundle.

## Prerequisites

- SB02 completed.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProjectPaths.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Grounding.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Deliverables

- Dedicated grounding service with typed results and adversarial path tests.

## Dependency Impact

- SB06, SB12, SB16, and SB18 depend on grounded final delivery semantics.

## Validation Depth

- Critical foundation with negative proof that unrelated or escaped targets do not trigger final delivery proof.
- Include failing-first or adversarial proof when behavior changes, passing proof, source assertions, anti-stub audit, changed-file hashes, classification, and proof-debt closure status.

## Implementation Steps

- Inspect the referenced source and nearby tests.
- Implement the smallest correct change set for this subbundle only.
- Update proof artifacts under bundle://proof/SB05/.

## Do Not Do

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths.
- Do not use docs-only changes to satisfy runtime proof requirements.

## Acceptance Checklist

- Deliverables are complete.
- Required tests or source assertions are recorded.
- Execution report gate rows are updated.
- Dedicated runtime service exists at repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExternalTargetGroundingService.cs.
- Adversarial escaped/prohibited path proof is recorded at bundle://proof/SB05/transcripts/failing-first.txt.

## Proof Required

- bundle://proof/SB05/manifest.md
- bundle://proof/SB05/semantic-invariants.md when the subbundle is critical or behavior-changing.
- Command transcripts under bundle://proof/SB05/transcripts/.

## Browser Validation Logging

- N/A unless UI surfaces grounded roots.
- Update bundle://reviews/01-execution-report.md if browser proof is applicable.

## Progression Gate

- SB06 may start because final-delivery grounding has adversarial proof in bundle://proof/SB05/transcripts/failing-first.txt.

## Closure Evidence

- Manifest: bundle://proof/SB05/manifest.md
- Semantic invariants: bundle://proof/SB05/semantic-invariants.md
- Source assertions: bundle://proof/SB05/transcripts/source-assertions.txt
- Failing-first/adversarial proof: bundle://proof/SB05/transcripts/failing-first.txt
- Passing tests: bundle://proof/SB05/transcripts/passing.txt
- Anti-stub audit: bundle://proof/SB05/transcripts/anti-stub-audit.txt
- Changed-file hashes: bundle://proof/SB05/transcripts/changed-file-hashes.txt
- Browser validation: N/A; runtime/prompt/metadata behavior only.

## Suggested Agent Prompt

- Execute SB05 literally, preserve runtime genericity, and close owned proof before moving downstream.
