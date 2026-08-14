# B01 — Execution primitives, environment semantics, and executable resolution

## Mission

Make one typed, OS-correct, lifecycle-owned process execution foundation before adapting Workbench, Manager, MCP, or plugins.

## Why now

The central workspace host is a good base, but environment and executable policies are Windows-first and another independent external-tool runner exists.

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

- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandProcessRunner.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceDotnetProcessLifecycle.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/WorkspaceProcessContracts.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandEnvironmentPolicy.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceExecutableLocator.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceGitCommandExecutor.cs`
- `{{REPO_ROOT}}/src/MAF/Tools/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.AgentFramework/Services/WorkspaceExternalProcessRunner.cs`

## Requirements

`EXEC-001`, `EXEC-002`, `EXEC-003`, `EXEC-004`, `EXEC-005`, `EXEC-006`, `EXEC-007`, `EXEC-008`

## Prerequisites

- `B00`
- `Gate R0`

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

`Governed` for B01 because it changes the P0 process execution, executable identity, environment, cancellation, lifecycle, and receipt boundary. Evidence must include semantic invariant assertions, focused Windows/Linux actual-host behavior, negative/security cases, dependency and source assertions, redaction, hashes, and independent review.

## Entry gate

- Status before execution: `Eligible — Gate R0 GO`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Gate R1a is GO for implementation on Windows/Linux actual-host tests plus deterministic macOS contract fixtures under `RUNTIME-MACOS-VALIDATION-001`; actual macOS proof remains deferred.
- One low-level process primitive and lifecycle owner are authoritative.
- Executable/environment semantics are OS-correct and security reviewed.
- No child process leak or secret-bearing receipt remains in tested paths.

## Status

- `Completed — Gate R1a GO`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
