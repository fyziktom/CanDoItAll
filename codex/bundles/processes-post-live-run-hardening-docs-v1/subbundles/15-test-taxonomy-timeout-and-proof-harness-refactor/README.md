# SB15: Test Taxonomy Timeout And Proof Harness Refactor

## Status

- Status: Completed

## Objective

- Refactor proof/test harness to avoid timeout-prone broad commands.

## Covered Inputs

- RN15 maps to RQ15.
- Preserve the original bundle scope for this subbundle.

## Prerequisites

- SB08 completed.

## Exact Source References

- repo://tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj
- repo://tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj
- repo://tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj
- bundle://scripts/validation-commands.md

## Deliverables

- Named validation commands, slow/live/browser separation, quarantine notes, and proof transcript collection.

## Dependency Impact

- SB18 final closure depends on durable proof collection.

## Validation Depth

- Critical foundation for final proof, requiring timeout-risk classification and passing named suites.
- Include failing-first or adversarial proof when behavior changes, passing proof, source assertions, anti-stub audit, changed-file hashes, classification, and proof-debt closure status.

## Implementation Steps

- Inspect the referenced source and nearby tests.
- Implement the smallest correct change set for this subbundle only.
- Update proof artifacts under bundle://proof/SB15/.

## Do Not Do

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths.
- Do not use docs-only changes to satisfy runtime proof requirements.

## Acceptance Checklist

- Deliverables are complete.
- Required tests or source assertions are recorded.
- Execution report gate rows are updated.

## Proof Required

- bundle://proof/SB15/manifest.md
- bundle://proof/SB15/semantic-invariants.md when the subbundle is critical or behavior-changing.
- Command transcripts under bundle://proof/SB15/transcripts/.

## Browser Validation Logging

- N/A.
- Update bundle://reviews/01-execution-report.md if browser proof is applicable.

## Progression Gate

- SB18 may start only after final proof commands are split and runnable.

## Suggested Agent Prompt

- Execute SB15 literally, preserve runtime genericity, and close owned proof before moving downstream.
