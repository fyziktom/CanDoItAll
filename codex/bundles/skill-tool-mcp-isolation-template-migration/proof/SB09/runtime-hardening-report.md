# SB09 Runtime Hardening Report

## Refactors Completed

- `MafAgentRuntime.Capabilities.Access.cs` was split into:
  - `MafAgentRuntime.Capabilities.Access.cs`
  - `MafAgentRuntime.Capabilities.Access.Policies.cs`
  - `MafAgentRuntime.Capabilities.Access.RuntimeToolDescriptors.cs`
  - `MafAgentRuntime.Capabilities.Access.CatalogDescriptors.cs`
- Configured workspace/tool-plugin attachment was split from `MafAgentRuntime.Capabilities.Tools.cs` into `MafAgentRuntime.Capabilities.Tools.ConfiguredWorkspace.cs`.
- The configured workspace attach method no longer checks `contextIntent.WorkspaceToolsEnabled` directly. Tool creation is filtered by `IsConfiguredRuntimeToolAllowed`, which reads `capabilityAccessPlan.InitialAllowedCapabilities`.

## Hidden Filter Review

- Removed raw configured workspace-tool attach suppression outside the shared evaluator.
- Remaining `WorkspaceToolsEnabled` usage is policy construction in `MafAgentRuntime.Capabilities.Access.Policies.cs` and execution metadata plumbing in Core.
- Remaining `RuntimeToolProvidersEnabled` usage in `MafAgentRuntime.Capabilities.RuntimeToolProviders.cs` is provider enumeration enablement. Provider-produced tools still flow through `EvaluateRuntimeToolAccess`.
- Remaining MCP `allowedTools` usage is a compatibility shim for child tools returned by `ListToolsAsync`; server-level MCP descriptors are evaluated before attach.

## Performance Review

- No SB09 changes introduced blocking waits, `Thread.Sleep`, `async void`, or sync-over-async in runtime access paths.
- No SB09 changes introduced repeated template parsing or repeated serializer option creation in hot access evaluation paths.
- Existing scan hits are outside the SB09 access split:
  - `MafAgentRuntime.AgentFactory.cs` debug/inspection synchronous reads.
  - Core workspace/path helpers with synchronous process stream reads.
  - Existing catalog and execution-state materialization.
  - Existing JSON option creation in Core validation/observability helpers.
- `ExternalProcessToolInvoker` still uses `ProcessStartInfo`, which is expected for external tool invocation and covered by diagnostics/timeout contract tests.

## Static Size Review

- New/refactored SB09 split files are below 500 lines:
  - `MafAgentRuntime.Capabilities.Access.CatalogDescriptors.cs`: 400
  - `MafAgentRuntime.Capabilities.Tools.cs`: 397
  - `MafAgentRuntime.Capabilities.Access.RuntimeToolDescriptors.cs`: 176
  - `MafAgentRuntime.Capabilities.Access.cs`: 174
  - `MafAgentRuntime.Capabilities.Access.Policies.cs`: 165
  - `MafAgentRuntime.Capabilities.Tools.ConfiguredWorkspace.cs`: 153
- Existing over-500 files remain accepted risks:
  - `MafAgentRuntime.Capabilities.cs`: 957
  - `MafAgentRuntime.Capabilities.Mcp.cs`: 651

## Validation Summary

- Runtime diagnostics contract suite: `46 passed`
- MAF runtime composition suite: `27 passed`
- Process/runtime capability filtering integration suite: `6 passed`
- MAF project build after split: `0 warnings`, `0 errors`
- Full solution build: `0 warnings`, `0 errors`
