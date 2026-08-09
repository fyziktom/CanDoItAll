# A03 — Storage, control-plane roots, and host-bound path migration

## Mission

Move storage and control-plane state onto the new path/filesystem contracts with transactional compatibility and rebind semantics.

## Why now

Absolute workspace/storage/application paths are persisted today and can become dangerous or unusable when profiles move between operating systems.

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

- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Configuration/AppOptions.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/ControlPlanePaths.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApplicationPreferences.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStorageDriver.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageBootstrapCatalogPolicy.cs`

## Requirements

`STO-001`, `STO-002`, `STO-003`, `STO-004`, `STO-005`, `STO-006`, `STO-007`, `STO-008`, `STO-009`

## Prerequisites

- `A02`
- `Gate C1`

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

- Status at execution entry: `Eligible — Gate C1 GO`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Every path-bearing persisted record is logical or explicitly host-bound/versioned.
- Old Windows locators and profiles have proven migration/rebind/rollback.
- Storage writes and bootstrap authority use the new filesystem semantics.
- Gate C2a is GO and A04 may begin.

## Status

- `Completed — Gate C2a GO`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
