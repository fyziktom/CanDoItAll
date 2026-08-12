# B03 — Manager process ownership, supervision, and watchers

## Mission

Make Manager recovery and supervision safe on Windows, Linux, and macOS without name-only termination or watcher assumptions.

## Why now

Current WMI behavior is Windows-specific and the Unix fallback lacks sufficient command identity; watcher behavior is only partially convergent.

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

- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/CanDoItAll.Manager.csproj`
- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs`
- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/WatchSupervisorService.cs`
- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/TailwindWatchSupervisorService.cs`
- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/TuningExecutionAdapter.cs`

## Requirements

`MGR-001`, `MGR-002`, `MGR-003`, `MGR-004`, `MGR-005`, `MGR-006`, `MGR-007`

## Prerequisites

- `B02`

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

- Status before execution: `Eligible — B02 Workbench gate GO`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Gate R2 is GO.
- No process is killed using name-only or ambiguous evidence.
- Windows/Linux/macOS recovery adapters and primary launched-process registry are proven.
- Supervisor/watcher pipelines converge after faults and shutdown.

## Status

- `Completed — Gate R2 GO`

## Handoff

Evidence, the primary recommendation, and independent approval are recorded in reviews 14–16. B04 is the only eligible next subbundle; actual macOS remains deferred under `RUNTIME-MACOS-VALIDATION-001`.
