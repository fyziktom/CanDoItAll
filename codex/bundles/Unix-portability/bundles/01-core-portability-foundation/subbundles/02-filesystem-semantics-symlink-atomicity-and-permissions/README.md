# A02 — Filesystem semantics, symlink safety, atomicity, and permissions

## Mission

Create a trustworthy filesystem foundation for storage and key material on Windows, Linux, and macOS.

## Why now

Case behavior, enumeration order, links, atomic writes, cross-process coordination, filenames, permissions, and watchers vary independently of slash syntax.

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

- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStoragePathPolicy.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStorageDriver.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureLocalFileOpener.cs`
- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/TailwindWatchSupervisorService.cs`

## Requirements

`FS-001`, `FS-002`, `FS-003`, `FS-004`, `FS-005`, `FS-006`, `FS-007`, `FS-008`, `FS-009`, `FS-010`

## Prerequisites

- `A01`

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

- Status before execution: `Blocked by A01`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Gate C1 is GO after independent architecture/security review.
- Filesystem semantics are deterministic and actual-host tested.
- Managed-root link escape and unsafe permission cases fail closed.
- Atomic/cross-process behavior is proven before storage or secrets migration.

## Status

- `Blocked by A01`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
