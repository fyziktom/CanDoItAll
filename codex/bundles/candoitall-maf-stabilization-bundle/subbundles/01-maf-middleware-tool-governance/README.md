# 01 - MAF Middleware and Tool Governance Hardening

## Objective

Create a central MAF-native policy plane for agent runs and function/tool invocation. The current runtime uses approval wrappers, MCP validation, telemetry middleware, and a post-stream repeated-tool guard. Codex must convert this into an explicit, testable policy layer that can inspect and control tool calls before execution where MAF allows it.

## Primary files to inspect


- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- Existing tests for runtime/tools/approvals, or create new unit tests if missing.


## Required implementation tasks


1. Introduce a central tool policy model, for example:
   - `ToolInvocationPolicyContext`
   - `ToolInvocationPolicyDecision`
   - `ToolInvocationDecisionKind` with values such as `Allow`, `Deny`, `RequireApproval`, `SanitizeResult`, `SkipExecution`.
   - `IAgentToolInvocationPolicy`.
2. Implement function invocation middleware using MAF function invocation middleware/runtime context APIs.
3. The middleware must inspect tool name, arguments, agent id, process id, run id, source kind, approval state, capability metadata, and tool classification.
4. Enforce at least these rules:
   - Unknown tools are denied unless explicitly allowed by capability composition.
   - Disabled built-in tools are never attached.
   - Workspace write/move/delete/append/copy/create-directory/dotnet-new/script execution require approval unless the run context explicitly allows auto-approval.
   - Local MCP tools respect allowed-tool lists and approval mode.
   - Hosted/provider-native tools respect provider capability matrix and documented approval limitations.
   - Repeated identical mutation or validation tool calls are blocked before another execution when the threshold is exceeded.
5. Replace `IsBuiltInToolEnabled(...) => true` with real config handling.
6. Keep existing approval wrappers, but treat middleware as the central guardrail. Approval wrappers are still useful because they integrate with MAF approval requests.
7. Emit structured telemetry/log entries for policy decisions with redacted arguments.
8. Do not store calculator-specific logic in the policy layer.


## Required tests


Unit tests:
- Disabled built-in tool configuration prevents tool attachment.
- Read-only workspace tools are allowed.
- Write/mutation workspace tools require approval or are denied when approval is unavailable.
- A repeated identical mutation tool call is blocked before tool execution.
- MCP tools outside `AllowedTools` are denied.
- Policy logs contain tool name, decision, agent id/run id, and no secrets.

Integration or component tests:
- A process run with auto-approved tools still executes approved workspace writes.
- A run without approval permission receives a pending approval or policy denial instead of executing a mutation.
- Existing calculator/process mock flow still passes.


## Risks and constraints


- MAF provider-native hosted tools may not expose the same approval/wrapping behavior as local function tools; handle these with capability gating and clear errors.
- Do not break existing approval request behavior.
- Do not log raw tool arguments when they may contain secrets.

