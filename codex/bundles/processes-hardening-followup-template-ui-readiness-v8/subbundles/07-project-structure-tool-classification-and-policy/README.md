# SB07: project-structure-tool-classification-and-policy

## Status

- Completed

## Closure Notes

- Registered project-structure read and mutation tool names in `AgentToolInvocationPolicyMetadata`.
- Required `ExecuteExternalAction` for project-structure mutation tools and preserved read-only handling for project-structure inspection tools.
- Changed unregistered `project_structure_*` tool classification to `Unknown` so new tool names cannot silently inherit generic read behavior.
- Added focused unit tests for mutation denial/allowance, read allowance, and the complete runtime project-structure tool inventory.
- Added projected-template governance coverage and operation contracts for screenshot/layout project-structure writeback templates.

## Objective

Make project-structure tools first-class governed external-action tools.

## Covered Inputs

- RQ06 project-structure tool governance
- F05 project-structure writeback tool classification

## Prerequisites

- SB06 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs

## Scope

- Inventory all `project_structure_*` tools available to agents.
- Register/classify project-structure mutation tools in `AgentToolInvocationPolicyMetadata`.
- Require `ExecuteExternalAction` for project-structure mutations and treat read-only project-structure inspection separately.
- Ensure template writeback steps include the correct operations.
- Add red-team tests proving validation or architecture steps cannot call project-structure mutation tools unless their contract allows it.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB07/.

## Implementation Steps

- Inventory all `project_structure_*` tools available to agents.
- Register/classify project-structure mutation tools in `AgentToolInvocationPolicyMetadata`.
- Require `ExecuteExternalAction` for project-structure mutations and treat read-only project-structure inspection separately.
- Ensure template writeback steps include the correct operations.
- Add red-team tests proving validation or architecture steps cannot call project-structure mutation tools unless their contract allows it.

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
- bundle://proof/SB07/manifest.md and bundle://proof/SB07/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB07/manifest.md.
- Semantic invariant contract: bundle://proof/SB07/semantic-invariants.md.
- Command transcripts: bundle://proof/SB07/transcripts/.

## Browser Validation Logging

- N/A for direct browser rendering unless implementation changes browser-visible behavior; still record the N/A decision in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Closure gate passes only after proof artifacts exist, referenced paths resolve, and downstream dependency impact is recorded.
- Dependent subbundle may start only after the closure gate is Completed or the blocker is explicit.

## Suggested Agent Prompt

- Execute SB07 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

This subbundle is not complete until the proof files under `proof/SB07` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
