# B06 — Process-domain driver and special-tool capability adaptation

## Mission

Connect host capabilities to process strategies and special/domain drivers while preserving Processes as the semantic owner.

## Why now

The current ProcessDriverLayer.Platform can support platform-aware strategy composition, but generic host primitives must not move into the process domain or MAF.

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

- `{{REPO_ROOT}}/src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverDescriptor.cs`
- `{{REPO_ROOT}}/src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterDescriptors.cs`
- `{{REPO_ROOT}}/src/Processes/CanDoItAll.Processes.Runtime`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration`
- `{{REPO_ROOT}}/codex/bundles/MAF-Refactor/adrs/ADR-007-process-semantics-owned-by-processes.md`

## Requirements

`PROC-001`, `PROC-002`, `PROC-003`, `PROC-004`, `PROC-005`, `PROC-006`

## Prerequisites

- `B05`

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

- Status before execution: `Eligible — Gate R3b GO`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Gate R3 is GO.
- Processes remains the semantic owner and MAF remains a generic execution adapter.
- Every special/domain driver declares and consumes host capabilities through approved boundaries.
- Unsupported profiles fail or choose alternatives deterministically before unsafe side effects.

## Status

- `Completed — Gate R3 GO`

The governed Windows/Linux evidence package is frozen under `artifacts/unix-portability/B06`, and independent review 25 records Gate R3 GO. Actual macOS and hosted CI remain explicit B07 boundaries under the operator's deferral instruction.

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
