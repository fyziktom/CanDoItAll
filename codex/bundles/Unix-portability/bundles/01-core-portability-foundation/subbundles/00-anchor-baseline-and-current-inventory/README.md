# A00 — Anchor, baseline, and current portability inventory

## Mission

Re-anchor the supplied plan to the exact execution checkout and produce a complete, classified inventory before product code changes.

## Why now

The prepared analysis is anchored to development commit 62ea8ee..., but the branch is active and the latest MAF/process refactor changed ownership boundaries and source paths.

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

- `{{REPO_ROOT}}/global.json`
- `{{REPO_ROOT}}/Directory.Build.props`
- `{{REPO_ROOT}}/CanDoItAll.slnx`
- `{{REPO_ROOT}}/.github/workflows-disabled/ci.yml`
- `{{REPO_ROOT}}/src/App/CanDoItAll.Web/Program.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Security/SecretVaults.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs`
- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs`
- `{{REPO_ROOT}}/codex/bundles/MAF-Refactor/adrs/ADR-007-process-semantics-owned-by-processes.md`

## Requirements

`PREP-001`, `PREP-002`, `PREP-003`, `PREP-004`

## Prerequisites

- Program entry and exact source anchor verification.

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

## C# Architecture Impact

A00 changes no product code. It establishes the enforceable ownership, dependency, pattern, partial-class, and testability constraints for A01-A07. See `architecture/00-csharp-current-state-inventory.md` through `architecture/04-csharp-testability-plan.md` and `plan/architecture-checkpoints.md`.

## Current Responsibility Inventory

The execution snapshot contains 103 projects and 608 project-reference edges with zero project-level cycles. Infrastructure owns physical filesystem/storage behavior; Security abstractions and implementation remain separate; MAF owns generic execution; Workbench owns runtime-node meaning/presentation; Manager owns supervision; Processes owns domain semantics; composition selects host capabilities.

## Target Responsibility Slicing

The only new core slice approved at C0 is a pure logical-path value in SharedKernel. It has no I/O or host probing. Physical path policy stays in Infrastructure, secret providers stay in Modules.Security, and runtime/process work remains blocked until C4.

## Pattern Decisions

Approved patterns are immutable typed path values, narrow capability contracts with composition-selected leaf adapters, versioned migration readers with backup/verify/commit/rollback, explicit secret-provider strategies, and durable same-directory atomic replacement. Broad platform services and silent fallbacks are rejected.

## Project/Dependency Direction

New references must point inward to existing contracts. A01 may use SharedKernel without introducing a reverse edge. Every changed graph is re-snapshotted and must retain zero project-level cycles. Temporary Components/FileTools project references are deferred to B00.

## Partial-Class Strategy

The current source has 171 partial declarations across 73 type names. No new partial split is approved. A changed cluster must extract an independently testable responsibility; moving methods between files is not an architectural improvement.

## Testability Contract

Each phase requires isolated behavior proof at the contract boundary plus actual-host characterization where OS behavior matters. Migrations expose restart/rollback state, secret tests never disclose values, filesystem tests use disposable roots, and process tests consume typed plans rather than shell strings.

## Proof tier

`Standard` for A00. It changes inventories and validation utilities only; security, migration, and process implementation phases use their stricter behavioral/governed proof requirements.

## Entry gate

- Status before execution: `Eligible — first executable subbundle`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Gate C0 is GO with an exact current commit.
- No unclassified P0/P1 finding or unknown persisted path/key record remains.
- Baseline failures are distinguished from implementation regressions.
- The first eligible implementation subbundle is A01 only.

## Status

- `Completed — Gate C0 GO`

## Handoff

Recorded in `reviews/A00-HANDOFF.md`. The only next eligible subbundle is A01.
