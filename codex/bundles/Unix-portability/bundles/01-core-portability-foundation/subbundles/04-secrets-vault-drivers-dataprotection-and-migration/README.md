# A04 — Secrets, vault drivers, Data Protection, and migration

## Mission

Provide truthful secure secret persistence on Windows, Linux, and macOS while preserving existing encrypted data.

## Why now

The original provider correction made strong vaults explicit but left a real first-launch contradiction: an authorized development file vault was rejected by its own startup probe. A supported, truthfully classified basic local tier is required alongside the stronger providers.

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

`SEC-001`, `SEC-002`, `SEC-003`, `SEC-004`, `SEC-005`, `SEC-006`, `SEC-007`, `SEC-008`, `SEC-009`, `SEC-010`, `SEC-011`, `SEC-012`, `SEC-013`, `SEC-014`

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

- No broad platform service, duplicate process/path/secret stack, silent or misrepresented security fallback, automatic Unix elevation, or name-only process kill. A basic local vault must expose its weaker protection level and a non-secret security notice.
- Use logical versus physical path contracts correctly.
- Keep MAF generic and process semantics in `Processes`.
- Use typed process arguments; shell only for explicitly modeled scripts.
- Keep source-code comments in English.

## Entry gate

- Status before execution: `C2a GO`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Gate C2 is GO from architect, security reviewer, and runtime validator.
- Auto never selects an unsupported or undeclared persistence tier.
- Production key material is protected at rest and permission-hardened.
- Legacy Windows secret/control-plane data has a tested migration and rollback path.

## Status

- `SEC-014 Windows DPAPI/Strong and Unix LocalUserFile/BasicLocal correction independently GO; Gate C2 remains blocked solely by genuine macOS proof`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
