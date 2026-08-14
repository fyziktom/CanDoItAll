# B91 — Conditional external dependency quarantine and recovery

## Mission

Contain an unsafe or unsupported FileTools, Docker, Node/Playwright, terminal, or native discovery dependency without weakening unrelated core/runtime behavior.

## Why now

External dependency failure must not force an insecure fallback or a false support claim.

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

- `{{REPO_ROOT}}/src/Integration/CanDoItAll.FileTools.Integration/CanDoItAll.FileTools.Integration.csproj`
- `{{REPO_ROOT}}/src/plugins/Implementations/CanDoItAll.Plugin.Docker/DockerHostToolService.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/PlaywrightMcpLaunchResolver.cs`

## Requirements

Conditional path; inherits requirements from the invoking gate.

## Prerequisites

- `Unverified or regressed external/native dependency`

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

- Unsafe dependency behavior is contained.
- Support claims match evidence.
- Affected gate is re-reviewed.

## Status

- `Not invoked`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
