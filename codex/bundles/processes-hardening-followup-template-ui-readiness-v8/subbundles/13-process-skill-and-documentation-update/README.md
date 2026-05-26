# SB13: 13-process-skill-and-documentation-update

## Goal

Update skills and documentation for new process governance.

## Required work

- Expand `codex/skills/candoitall-api-processes/SKILL.md` with typed operation contracts, target scopes, contract mode, block/recovery, projection lineage, workflow/subprocess mappings, and template guidance.
- Add concrete API examples for save/import/export/start/transition/artifact record.
- Add Tetris Blazor WASM PWA process-run checklist.
- Document that Workflows are role executors under Processes, not replacements for Processes.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB13` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
