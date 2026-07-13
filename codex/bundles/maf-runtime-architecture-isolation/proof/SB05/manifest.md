# SB05 Manifest

## Status

- Result: `Partial`
- Scope: workspace/MCP dependency fallback isolation and storage driver lift.

## Evidence

- Workspace file, command, and artifact services are resolved through `IMafRuntimeDependencyResolver`.
- `StorageRuntimePlugin` is now a top-level internal driver instead of a nested `MafAgentRuntime` private class.
- Local MCP command execution fallback now uses resolved workspace services instead of constructing command services directly in the MCP partial.

## Production Behavior Artifact Matrix

| Artifact | Production Path | Status |
| --- | --- | --- |
| Workspace service fallback | `MafRuntimeDependencyResolver.ResolveWorkspaceServices` | Used |
| Storage runtime plugin | `StorageRuntimePlugin` | Top-level internal driver |
| Local MCP command execution service | `MafAgentRuntime.Capabilities.Mcp.cs` | Routed through resolver |

## Residual

- `WorkspaceRuntimePlugin`, context providers, skills, built-in tools, and most MCP feature-driver logic remain in `MafAgentRuntime` partials.
