# B00 — Core handoff anchor, ownership review, and runtime inventory

## Mission

Rebase the runtime plan to the exact core-portability commit and reapprove ownership before touching process/runtime code.

## Why now

The runtime source is broad and likely to change while the core bundle lands. The latest MAF refactor also makes ownership errors more damaging than ordinary portability defects.

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

- `{{REPO_ROOT}}/codex/bundles/MAF-Refactor/adrs/ADR-007-process-semantics-owned-by-processes.md`
- `{{REPO_ROOT}}/codex/bundles/MAF-Refactor/architecture/15-exact-code-adaptation-inventory.md`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs`
- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs`
- `{{REPO_ROOT}}/src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverDescriptor.cs`

## Requirements

`RPREP-001`, `RPREP-002`, `RPREP-003`, `RPREP-004`

## Prerequisites

- `Core Gate C4 or operator-authorized provisional core handoff`

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

## Proof tier

`Behavioral` for B00. This subbundle changes inventory and gate artifacts only, but it must characterize the current process/runtime boundaries on Windows and Linux with named tests. It does not require another full solution run because the exact accepted anchors already have unchanged 7,459/7,459 Windows and Linux aggregate evidence.

## Entry gate

- Status before execution: `Eligible — provisional core handoff accepted for implementation`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Gate R0 is GO against the exact accepted core implementation anchors.
- One owner exists for every runtime responsibility and no process-semantic rule is assigned to MAF/Infrastructure.
- Split triggers were evaluated and recorded.
- B01 is the only eligible implementation subbundle.

## Status

- `Completed — Gate R0 GO`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
