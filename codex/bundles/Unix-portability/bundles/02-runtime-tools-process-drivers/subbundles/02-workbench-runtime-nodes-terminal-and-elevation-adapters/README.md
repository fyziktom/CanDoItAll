# B02 — Workbench runtime nodes, terminal presentation, and elevation adapters

## Mission

Replace the Windows/PowerShell runtime-node launcher with typed direct execution and optional platform presentation adapters.

## Why now

ProjectStructureRuntimeLauncher currently renders every runtime intent as PowerShell and disables the feature outside Windows.

## Scope

- Execute only the tasks and requirements owned by this subbundle.
- Update affected source references, findings, requirements, ADRs, validation, and evidence.
- Preserve established architecture and migration compatibility.

## Out of scope

- Downstream subbundle implementation.
- Opportunistic unrelated cleanup.
- Changes to external repositories/packages unless this subbundle explicitly invokes a split/quarantine path.
- Commit, push, or PR publication without explicit operator instruction.

## Source hotspots

- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureDirectDotNetCommandPolicy.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureLocalFileOpener.cs`

## Requirements

`NODE-001`, `NODE-002`, `NODE-003`, `NODE-004`, `NODE-005`, `NODE-006`, `NODE-007`, `NODE-008`

## Prerequisites

- `B01`

## Deliverables

- Production and test changes limited to this scope.
- Failing-first or named characterization proof.
- Updated evidence and gate report.
- Updated source/finding/requirement traceability.
- Redaction scan result.
- Session handoff.

## Architecture constraints

- No broad platform service, duplicate process/path/secret stack, insecure fallback, automatic Unix elevation, or name-only process kill.
- Use logical versus physical path contracts correctly.
- Keep MAF generic and process semantics in `Processes`.
- Use typed process arguments; shell only for explicitly modeled scripts.
- Keep source-code comments in English.

## Entry gate

- Status before execution: `Blocked by B01`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Runtime-node plans are typed and shell-neutral.
- Direct headless execution works on Windows/Linux/macOS.
- Terminal and elevation are truthful optional capabilities.
- Legacy metadata has a bounded migration/repair path and UI proof.

## Status

- `Blocked by B01`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
