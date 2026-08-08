# A92 — Conditional path and storage data recovery

## Mission

Recover control-plane/storage state after incorrect path conversion, host rebind, partial move, or catalog corruption.

## Why now

Blind separator fixes can corrupt logical identifiers or relocate physical data.

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

- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStorageDriver.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageBootstrapCatalogPolicy.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs`

## Requirements

Conditional path; inherits requirements from the invoking gate.

## Prerequisites

- `Any path/storage migration incident`

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

- Status before execution: `Not invoked`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Catalog and physical content agree.
- No trusted-root escape or silent data loss remains.
- The failed storage gate is re-reviewed.

## Status

- `Not invoked`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
