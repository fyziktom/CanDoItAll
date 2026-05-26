# SB06: Refactor Checkpoint A Template Skill Contracts

## Status

- Status: `Completed`

## Objective

Run a checkpoint after template and skill updates to remove duplication, drift, and topic-specific leakage before UI/API preflight work.

## Covered Inputs

- RQ03 template boundaries.
- RQ05 reusable skills.
- RQ09 drift checks.

## Prerequisites

- SB05 skills and proof guidance are complete.

## Exact Source References

- `repo://Templates/Processes/processes/blazor-app-delivery/definition.json`
- `repo://Templates/Processes/processes/blazor-app-delivery/definition.md`
- `repo://Templates/Processes/README.md`
- `repo://codex/skills/candoitall-api-processes/SKILL.md`

## Deliverables

- Source assertion report for duplicated or drifting Blazor WASM PWA requirements.
- Small cleanup changes only where they remove real duplication or contradiction.

## Dependency Impact

- SB07 UI/API preflight depends on stable template/skill terminology.

## Validation Depth

- Source-level audit and focused regression tests affected by any cleanup.

## Implementation Steps

1. Compare template, skill, and README terminology.
2. Remove contradictions or topic-specific leakage.
3. Avoid broad refactors unless required to make downstream preflight clear.

## Do Not Do

- Do not add new process layers.
- Do not change runtime behavior without a test.

## Acceptance Checklist

- Generic Blazor WASM PWA terminology is consistent.
- No reusable instruction contains app-topic-specific acceptance criteria.
- Downstream source references remain valid.

## Proof Required

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- `proof/SB06/transcripts/source-assertions.txt`
- `proof/SB06/transcripts/passing.txt`

## Browser Validation Logging

- N/A. Checkpoint is source and contract focused.

## Progression Gate

- SB07 may start after checkpoint assertions pass.

## Suggested Agent Prompt

Run the template/skill contract checkpoint and clean only contradictions that would weaken generic Blazor WASM PWA preflight.
