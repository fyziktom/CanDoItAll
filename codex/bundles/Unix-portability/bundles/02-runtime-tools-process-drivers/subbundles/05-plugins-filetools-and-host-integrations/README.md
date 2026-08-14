# B05 — Plugins, FileTools, and host integrations

## Mission

Adapt Docker, desktop opening, FileTools, and other external/native integrations without making unverified dependencies part of the core support claim.

## Why now

Docker constructs its own process host and FileTools behavior comes from a pinned package outside the repository.

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

- `{{REPO_ROOT}}/src/plugins/Implementations/CanDoItAll.Plugin.Docker/DockerHostToolService.cs`
- `{{REPO_ROOT}}/src/Integration/CanDoItAll.FileTools.Integration/CanDoItAll.FileTools.Integration.csproj`
- `{{REPO_ROOT}}/src/Integration/CanDoItAll.FileTools.Integration/ConfiguredDesktopFileLauncher.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureLocalFileOpener.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApplicationPreferences.cs`

## Requirements

`PLUG-001`, `PLUG-002`, `PLUG-003`, `PLUG-004`, `PLUG-005`

## Prerequisites

- `B04`

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

- Status before execution: `Completed — Gate R3b GO`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Gate R3b is GO.
- Docker and plugin tools use authoritative host execution and capability probes.
- FileTools support claims are backed by a pinned compatibility report.
- Desktop integrations are optional, host-bound, and disabled in headless/service profiles.

## Status

- `Completed — Gate R3b GO`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
