# SB17: Refactor Checkpoint B Docs And Template Parity

## Status

- Status: Completed

## Objective

- Docs/template parity checkpoint before final closure.

## Covered Inputs

- RN12 maps to RQ12.
- Preserve the original bundle scope for this subbundle.

## Prerequisites

- SB11 and SB12 completed.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs
- repo://Templates/Processes/README.md
- repo://codex/skills/candoitall-api-processes/SKILL.md

## Deliverables

- Docs/skills/templates/API examples compared against source enums and DTOs, with template validation.

## Dependency Impact

- SB18 final release readiness relies on source-aligned docs.

## Validation Depth

- Docs/template parity proof.
- Include failing-first or adversarial proof when behavior changes, passing proof, source assertions, anti-stub audit, changed-file hashes, classification, and proof-debt closure status.

## Implementation Steps

- Inspect the referenced source and nearby tests.
- Implement the smallest correct change set for this subbundle only.
- Update proof artifacts under bundle://proof/SB17/.

## Do Not Do

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths.
- Do not use docs-only changes to satisfy runtime proof requirements.

## Acceptance Checklist

- Deliverables are complete.
- Required tests or source assertions are recorded.
- Execution report gate rows are updated.

## Proof Required

- bundle://proof/SB17/manifest.md
- bundle://proof/SB17/semantic-invariants.md when the subbundle is critical or behavior-changing.
- Command transcripts under bundle://proof/SB17/transcripts/.

## Browser Validation Logging

- N/A.
- Update bundle://reviews/01-execution-report.md if browser proof is applicable.

## Progression Gate

- SB18 may start after parity proof passes.

## Suggested Agent Prompt

- Execute SB17 literally, preserve runtime genericity, and close owned proof before moving downstream.
