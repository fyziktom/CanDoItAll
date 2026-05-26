# SB15: Refactor Checkpoint B UI Test Readiness

## Status

- Status: `Completed`

## Objective

Run a final checkpoint before closure to ensure UI/API/test code names are generic: live-run profile, scenario acceptance, operation contract, current-run proof, and artifact lineage.

## Covered Inputs

- RQ08 generic runtime breadth.
- RQ09 scope/template/fake-proof drift.

## Prerequisites

- SB14 harness/runbook preparation is complete.

## Exact Source References

- `repo://Templates/Processes`
- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`
- `repo://src/CanDoItAll.Modules.Processes`
- `repo://tests`
- `repo://codex/skills/candoitall-api-processes/SKILL.md`

## Deliverables

- Source assertion report for generic names and topic-neutral reusable instructions.
- Minimal cleanup for naming or proof drift found during SB14.

## Dependency Impact

- SB16 final closure depends on a clean genericity and proof-readiness checkpoint.

## Validation Depth

- Source assertions plus focused tests affected by any cleanup.

## Implementation Steps

1. Search process templates, skills, tests, and source for app-topic-specific reusable instructions.
2. Repair only reusable instructions, names, or tests that violate generic scope.
3. Confirm profile and proof terminology is consistent.

## Do Not Do

- Do not rewrite large modules for naming polish.
- Do not remove raw input preservation from the bundle.

## Acceptance Checklist

- Reusable runtime/template/skill instructions are generic.
- Tests name generic behavior rather than a demonstration topic.
- Final runbook can accept any Blazor WASM PWA topic.

## Proof Required

- `proof/SB15/manifest.md`
- `proof/SB15/semantic-invariants.md`
- `proof/SB15/transcripts/source-assertions.txt`
- `proof/SB15/transcripts/passing.txt`

## Browser Validation Logging

- N/A unless UI labels or process workspace behavior changes.

## Progression Gate

- SB16 may start after the genericity checkpoint passes.

## Suggested Agent Prompt

Run the final genericity and UI-test-readiness checkpoint, repair narrow drift only, and record source assertions.
