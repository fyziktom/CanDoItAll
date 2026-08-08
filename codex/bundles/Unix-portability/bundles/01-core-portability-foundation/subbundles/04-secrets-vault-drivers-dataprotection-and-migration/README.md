# A04 — Secrets, vault drivers, Data Protection, and migration

## Mission

Provide truthful secure secret persistence on Windows, Linux, and macOS while preserving existing encrypted data.

## Why now

Auto currently selects unsupported macOS/Linux vaults, the file-vault master key is stored beside ciphertext, and three key/payload systems must migrate together.

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

- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Security/SecretVaults.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Security/SecurityModels.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Security/SecurityModuleServiceCollectionExtensions.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs`

## Requirements

`SEC-001`, `SEC-002`, `SEC-003`, `SEC-004`, `SEC-005`, `SEC-006`, `SEC-007`, `SEC-008`, `SEC-009`, `SEC-010`, `SEC-011`, `SEC-012`, `SEC-013`

## Prerequisites

- `A03`

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

- Status before execution: `Blocked by A03`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Gate C2 is GO from architect, security reviewer, and runtime validator.
- Auto never selects unsupported or insecure persistence.
- Production key material is protected at rest and permission-hardened.
- Legacy Windows secret/control-plane data has a tested migration and rollback path.

## Status

- `Blocked by A03`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
