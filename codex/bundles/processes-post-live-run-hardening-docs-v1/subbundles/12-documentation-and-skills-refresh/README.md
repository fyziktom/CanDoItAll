# SB12: Documentation And Skills Refresh

## Status

- Status: Completed

## Objective

- Refresh documentation and Codex skills for the current process runtime.

## Covered Inputs

- RN12 maps to RQ12.
- Preserve the original bundle scope for this subbundle.

## Prerequisites

- SB05 and SB10 completed for current grounding/tool policy, or docs mark unavailable runtime blockers.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/README.md
- repo://Templates/Processes/README.md
- repo://codex/skills/candoitall-api-processes/SKILL.md
- repo://src/CanDoItAll.AgentFramework.Maf/README.md

## Deliverables

- Expanded module docs, template README, API process skill, MAF notes, and docs source assertions.

## Dependency Impact

- SB17 and SB18 rely on current docs and skills.

## Validation Depth

- Docs proof with source assertions and anti-stub audit.
- Include failing-first or adversarial proof when behavior changes, passing proof, source assertions, anti-stub audit, changed-file hashes, classification, and proof-debt closure status.

## Implementation Steps

- Inspect the referenced source and nearby tests.
- Implement the smallest correct change set for this subbundle only.
- Update proof artifacts under bundle://proof/SB12/.

## Do Not Do

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths.
- Do not use docs-only changes to satisfy runtime proof requirements.

## Acceptance Checklist

- Deliverables are complete.
- Required tests or source assertions are recorded.
- Execution report gate rows are updated.
- Closed with bundle://proof/SB12/manifest.md and bundle://proof/SB12/semantic-invariants.md.

## Proof Required

- bundle://proof/SB12/manifest.md
- bundle://proof/SB12/semantic-invariants.md when the subbundle is critical or behavior-changing.
- Command transcripts under bundle://proof/SB12/transcripts/.

## Browser Validation Logging

- N/A unless docs are rendered in-app.
- Update bundle://reviews/01-execution-report.md if browser proof is applicable.

## Progression Gate

- SB17 may start once docs are source-asserted.

## Suggested Agent Prompt

- Execute SB12 literally, preserve runtime genericity, and close owned proof before moving downstream.
