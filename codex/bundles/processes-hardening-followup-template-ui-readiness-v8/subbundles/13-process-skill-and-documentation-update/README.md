# SB13: process-skill-and-documentation-update

## Status

- Completed

## Objective

Update skills and documentation for new process governance.

## Covered Inputs

- RQ05 API/tool/skill parity
- F04 Processes API skill is shallow

## Prerequisites

- SB12 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://codex/skills/candoitall-api-processes/SKILL.md
- repo://Templates/Processes/README.md

## Scope

- Expand `codex/skills/candoitall-api-processes/SKILL.md` with typed operation contracts, target scopes, contract mode, block/recovery, projection lineage, workflow/subprocess mappings, and template guidance.
- Add concrete API examples for save/import/export/start/transition/artifact record.
- Add Tetris Blazor WASM PWA process-run checklist.
- Document that Workflows are role executors under Processes, not replacements for Processes.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB13/.

## Implementation Steps

- Expand `codex/skills/candoitall-api-processes/SKILL.md` with typed operation contracts, target scopes, contract mode, block/recovery, projection lineage, workflow/subprocess mappings, and template guidance.
- Add concrete API examples for save/import/export/start/transition/artifact record.
- Add Tetris Blazor WASM PWA process-run checklist.
- Document that Workflows are role executors under Processes, not replacements for Processes.

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
- bundle://proof/SB13/manifest.md and bundle://proof/SB13/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB13/manifest.md.
- Semantic invariant contract: bundle://proof/SB13/semantic-invariants.md.
- Command transcripts: bundle://proof/SB13/transcripts/.

## Browser Validation Logging

- N/A for direct browser rendering unless implementation changes browser-visible behavior; still record the N/A decision in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Closure gate passes only after proof artifacts exist, referenced paths resolve, and downstream dependency impact is recorded.
- Dependent subbundle may start only after the closure gate is Completed or the blocker is explicit.

## Suggested Agent Prompt

- Execute SB13 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

This subbundle is not complete until the proof files under `proof/SB13` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.

## Closure Notes

- Closed with repo and active Codex skill-root updates for `candoitall-api-processes`.
- Documented typed operation contracts, target scopes, contract mode, block/recovery health, projection lineage, workflow/subprocess artifact mappings, concrete API examples, Tetris Blazor WASM PWA checklist, and workflow-as-executor boundaries.
- No browser validation is required for this documentation/skill phase.
