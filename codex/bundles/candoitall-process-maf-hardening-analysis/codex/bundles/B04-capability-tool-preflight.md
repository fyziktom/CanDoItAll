# B04 — Exact capability and runtime tool preflight

## Goal

Fail early and concretely when a step cannot access a required runtime tool. Do not start an LLM run just to discover a missing/denied tool.

## Current gap

Readiness checks classify tool names and check agent metadata. Actual tool execution can still fail because provider composition/scoped access depends on:

- governed process context,
- process run id,
- step id,
- audit scope,
- project id,
- allowed operations,
- runtime provider registration.

## Required changes

### 1. Build exact required tool list

Collect required tools from:

- template `CapabilityScope.RequiredReceipts`,
- launch context required receipt variables,
- subprocess contract if agent-owned fallback is enabled,
- validation/mutation tool requirements,
- runtime-owned tools separately.

### 2. Preflight composed providers

Add a service around dispatch:

```csharp
public interface IProcessRuntimeToolPreflightService
{
    ValueTask<ProcessRuntimeToolPreflightResult> CheckAsync(
        ProcessRuntimeToolPreflightRequest request,
        CancellationToken cancellationToken = default);
}
```

The service should check actual composed runtime tool descriptors for the selected agent/context.

### 3. Distinguish missing vs denied

Return concrete diagnostics:

- `process.runtime.required_tool_not_composed`
- `process.runtime.required_tool_denied`
- `process.runtime.required_tool_missing_provider`
- `process.runtime.required_tool_missing_agent_capability`
- `process.runtime.required_tool_missing_process_scope`

### 4. Apply before claim/agent execution

When preflight fails:

- do not call `AgentFrameworkWorkspaceExecutionService.ExecuteRunAsync`,
- submit `NeedsManager` with exact diagnostic,
- suggest rebind/grant/provider registration/action.

## Tests

- `Dispatch_WhenProjectStructureLaunchToolNotComposed_BlocksBeforeAgentRun`
- `Dispatch_WhenWorkspaceDotnetBuildMissing_BlocksBeforeAgentRun`
- `Dispatch_WhenToolDenied_ShowsDeniedDiagnosticWithScope`
- `Dispatch_WhenToolAvailable_AllowsAgentRun`

## Acceptance criteria

- Missing `project_structure_process_subprocess_launch` no longer appears as a vague agent failure.
- Operator message names the exact tool and whether the problem is composition, permission, provider or agent capability.
