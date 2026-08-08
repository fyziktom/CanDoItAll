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

- `Eligible — first executable subbundle`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
