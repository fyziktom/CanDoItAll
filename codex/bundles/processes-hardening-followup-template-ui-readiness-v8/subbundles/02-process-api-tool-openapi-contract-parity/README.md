# SB02: process-api-tool-openapi-contract-parity

## Status

- Completed

## Objective

Ensure public API/tool surfaces expose the new runtime governance model.

## Covered Inputs

- RQ05 API/tool/skill parity
- F04 Processes API governance surface

## Prerequisites

- SB01 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://src/CanDoItAll.Web/Api/ProcessesApi.cs
- repo://codex/skills/candoitall-api-processes/SKILL.md

## Scope

- Inventory `ProcessesApi`, MAF process tools, request/response DTOs, read models, import/export envelopes, and Swagger/OpenAPI-visible schemas.
- Verify that API save/import/export round-trips `ContractMode`, `AllowedOperations`, `OperationTargetScope`, artifact workflow mapping fields, subprocess child mapping fields, `BlockCause`, `ProjectionLineage`, `ProjectionIdentityHash`, recovery options, and block reason codes.
- Add API integration tests that write through nested routes and read the values back from run/detail endpoints.
- Ensure MAF process tools use the same model behavior as HTTP API.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB02/.

## Implementation Steps

- Inventory `ProcessesApi`, MAF process tools, request/response DTOs, read models, import/export envelopes, and Swagger/OpenAPI-visible schemas.
- Verify that API save/import/export round-trips `ContractMode`, `AllowedOperations`, `OperationTargetScope`, artifact workflow mapping fields, subprocess child mapping fields, `BlockCause`, `ProjectionLineage`, `ProjectionIdentityHash`, recovery options, and block reason codes.
- Add API integration tests that write through nested routes and read the values back from run/detail endpoints.
- Ensure MAF process tools use the same model behavior as HTTP API.

## Scope Exceptions

- None planned. Any discovered exception must be recorded as a blocker, reopened subbundle, or concrete follow-up before closure.

## Do Not Do

- Do not hardcode Tetris behavior into generic process runtime code.
- Do not introduce SQLite paths or non-PostgreSQL persistence assumptions.
- Do not replace runtime proof with source-text-only assertions for behavior-changing work.
- Do not silently narrow raw notes that say all, every, must, or same flow.

## Acceptance Checklist

- Required work is implemented or explicitly blocked with a follow-up.
- Targeted tests and relevant audit commands pass.
- bundle://proof/SB02/manifest.md and bundle://proof/SB02/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB02/manifest.md.
- Semantic invariant contract: bundle://proof/SB02/semantic-invariants.md.
- Command transcripts: bundle://proof/SB02/transcripts/.

## Browser Validation Logging

- N/A for direct browser rendering unless implementation changes browser-visible behavior; still record the N/A decision in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Closure gate completed on 2026-05-26. Proof artifacts exist under bundle://proof/SB02/, referenced source paths resolve, and SB03 may rely on API/tool/import-export contract parity.
- Dependent subbundle may start only after the closure gate is Completed or the blocker is explicit.

## Suggested Agent Prompt

- Execute SB02 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

This subbundle is not complete until the proof files under `proof/SB02` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
