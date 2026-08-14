# B04 — MCP local stdio and external tool runtime

## Mission

Adapt local MCP and external tools to the authoritative execution, executable, environment, secret, and lifecycle contracts.

## Why now

MCP resolution is partly OS-aware but diverges from workspace policy; Playwright scans global cache; external tools have another runner and can leak output.

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

- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/LocalMcpCommandPolicy.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceExecutableAuthorizationPolicy.cs`
- `{{REPO_ROOT}}/src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/LocalStdioMcpProcessLauncher.cs`
- `{{REPO_ROOT}}/src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/LocalStdioMcpEnvironmentBinder.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/PlaywrightMcpLaunchResolver.cs`
- `{{REPO_ROOT}}/src/MAF/Tools/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs`

## Requirements

`MCP-001`, `MCP-002`, `MCP-003`, `MCP-004`, `MCP-005`, `TOOL-001`, `TOOL-002`

## Prerequisites

- `B03`
- `Gate R2`

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

- Status before execution: `Eligible — Gate R2 GO`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Gate R3a is GO.
- Local MCP/external tools use authoritative execution and secret boundaries.
- Production Playwright MCP no longer depends on global cache discovery.
- Outputs and diagnostics pass redaction and cleanup tests.

## Status

- `Completed — Gate R3a GO`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
