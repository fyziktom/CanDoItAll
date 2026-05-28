# SB11: API OpenAPI And Process Tools Parity

## Status

- Status: Completed

## Objective

- Keep HTTP API, process tools, OpenAPI, and runtime models aligned.

## Covered Inputs

- RN11 maps to RQ11.
- Preserve the original bundle scope for this subbundle.

## Prerequisites

- SB03 and SB10 completed or explicitly not required for API parity.

## Exact Source References

- repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs
- repo://src/CanDoItAll.Modules.Processes/ImportExport/ProcessImportExportModels.cs
- repo://codex/skills/candoitall-api-processes/SKILL.md

## Deliverables

- API DTO/read-model parity audit, updated examples, and API integration tests for changed fields.

## Dependency Impact

- SB17 and SB18 rely on API/docs parity.

## Validation Depth

- API integration proof and source assertions.
- Include failing-first or adversarial proof when behavior changes, passing proof, source assertions, anti-stub audit, changed-file hashes, classification, and proof-debt closure status.

## Implementation Steps

- Inspect the referenced source and nearby tests.
- Implement the smallest correct change set for this subbundle only.
- Update proof artifacts under bundle://proof/SB11/.

## Do Not Do

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths.
- Do not use docs-only changes to satisfy runtime proof requirements.

## Acceptance Checklist

- Deliverables are complete.
- Required tests or source assertions are recorded.
- Execution report gate rows are updated.
- Closed with bundle://proof/SB11/manifest.md and bundle://proof/SB11/semantic-invariants.md.

## Proof Required

- bundle://proof/SB11/manifest.md
- bundle://proof/SB11/semantic-invariants.md when the subbundle is critical or behavior-changing.
- Command transcripts under bundle://proof/SB11/transcripts/.

## Browser Validation Logging

- N/A.
- Update bundle://reviews/01-execution-report.md if browser proof is applicable.

## Progression Gate

- SB17 may start after API/tool parity proof passes.

## Suggested Agent Prompt

- Execute SB11 literally, preserve runtime genericity, and close owned proof before moving downstream.
