# A01 — Logical path contract and portable configuration cleanup

## Mission

Fix the lowest-level slash, root, and path-category semantics before storage, secrets, or runtime changes.

## Why now

Current Infrastructure, MAF workspace, and MAF runtime path policies disagree; shared development configuration is Windows-only.

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

- `{{REPO_ROOT}}/src/App/CanDoItAll.Web/appsettings.Development.json`
- `{{REPO_ROOT}}/src/App/CanDoItAll.Web/Properties/launchSettings.json`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/ControlPlanePaths.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStoragePathPolicy.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Paths/WorkspacePathPolicy.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimePathResolver.cs`

## Requirements

`PATH-001`, `PATH-002`, `PATH-003`, `PATH-004`, `PATH-005`, `PATH-006`, `PATH-007`, `PATH-008`, `PATH-009`, `PATH-010`

## Prerequisites

- `A00`

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

- Status before execution: `Eligible — Gate C0 GO`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- All path categories and ownership boundaries are documented and tested.
- New logical path writers are host-independent and legacy readers are field-scoped.
- Linux/macOS development roots no longer depend on %LOCALAPPDATA% or backslashes.
- Gate C1a is GO; A02 is the only next mandatory subbundle.

## Status

- `Completed — Gate C1a GO`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
