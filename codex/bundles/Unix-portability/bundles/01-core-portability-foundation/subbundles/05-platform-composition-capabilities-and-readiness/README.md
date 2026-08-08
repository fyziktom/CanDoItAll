# A05 — Platform composition, capabilities, and readiness

## Mission

Wire the proven path/filesystem/storage/security implementations through narrow composition and truthful capability diagnostics.

## Why now

The repository needs OS-dependent selection, but a broad platform service or process-semantic leakage would undo the recent architecture refactor.

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

- `{{REPO_ROOT}}/src/App/CanDoItAll.Web/Program.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Security/SecurityModuleServiceCollectionExtensions.cs`
- `{{REPO_ROOT}}/codex/bundles/MAF-Refactor/adrs/ADR-007-process-semantics-owned-by-processes.md`

## Requirements

`PLAT-001`, `PLAT-002`, `PLAT-003`, `PLAT-004`, `PLAT-005`

## Prerequisites

- `A04`
- `Gate C2`

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

- Status before execution: `Blocked by Gate C2`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Mandatory providers are selected truthfully and optional capabilities degrade independently.
- No giant platform abstraction or process-semantic leakage was introduced.
- All target profile composition tests pass.
- Gate C3a is GO.

## Status

- `Blocked by Gate C2`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
