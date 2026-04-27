# Codex Master Prompt: CanDoItAll MAF Stabilization

You are a senior C#/.NET architect and senior Microsoft Agent Framework engineer.

You are working in the CanDoItAll repository. Your job is to stabilize the existing Microsoft Agent Framework architecture without replacing the already-working process-driven multi-agent system.

The current system already has important advanced pieces:

- Direct Microsoft Agent Framework usage.
- Process-driven multi-agent automation.
- Typed agent output contracts.
- MAF `ChatResponseFormat.ForJsonSchema(...)` structured response configuration.
- Tool approval wrappers.
- MCP capabilities.
- Session serialization.
- Approval checkpoint storage.
- Process dispatcher retries, provider fallback, artifact validation, and tool receipt validation.
- Existing calculator process proof through multiple cooperating agents.

Your work must improve stability, not rewrite everything.

## Primary mission

Implement the subbundles in order. For each subbundle:

1. Inspect the referenced files and related code paths.
2. Confirm whether the issue still exists in the working tree.
3. Implement the smallest correct change.
4. Add or update tests.
5. Run relevant build/test commands.
6. Produce an implementation report.

## Non-negotiable rules

- All source-code comments must be in English.
- Do not parse workflow decisions from markdown.
- Do not persist invalid agent output as successful workflow state.
- Do not drop structured-output contracts across continuations.
- Do not rely only on prompt instructions for machine-critical output.
- Do not introduce broad unrelated refactors.
- Do not remove working process automation behavior.
- Do not expose raw secrets in logs/traces.
- Do not silently swallow invalid outputs or tool-policy violations.
- If Microsoft Agent Framework API names differ from documentation, adapt to the installed package and document the difference.

## Repository files to inspect first

Start with:

- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `src/CanDoItAll.AgentFramework.Core/Execution/ExecutionCheckpointServices.cs`
- `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidation.cs`
- `src/CanDoItAll.AgentFramework.Models/OutputContracts/AgentOutputContracts.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.*.cs`
- `docs/agent-output-contracts.md`

Also inspect tests and prior Codex bundles for established conventions.

## First search commands

Run searches equivalent to:

```bash
rg -n "StructuredOutput|ResponseFormat|ForJsonSchema|RunAsync<|AgentRunOptions|ChatOptions" src docs tests
rg -n "ApprovalRequiredAIFunction|ToolApprovalRequestContent|AIFunctionFactory|FunctionInvocation|Use\(" src docs tests
rg -n "structuredOutput: null|PendingApprovals|RespondToPendingApprovalsAsync|ContinueExecutionRunAsync" src docs tests
rg -n "IAgentOutputValidator|AgentOutputValidationResult|DeserializeAndValidate|ProcessStepOutcomeResult" src docs tests
rg -n "Checkpoint|Workflow|Orchestration|AgentSession|SerializeSession|DeserializeSession|Compaction" src docs tests
rg -n "calculator process|If this is the calculator|IsBuiltInToolEnabled|SupportsStructuredOutput" src docs tests
```

If `tests` does not exist in the local checkout, search all available project directories and document the limitation.

## Implementation reporting format

After each subbundle, report:

```text
Subbundle: <name>
Status: Completed / Partially completed / Blocked
Files changed:
- ...
Tests run:
- command: result
Key behavior changes:
- ...
Remaining risks:
- ...
```

## Final acceptance

The stabilization is complete only when:

- The solution builds.
- Relevant unit tests pass.
- Relevant process/agent integration tests pass or have documented environment limitations.
- Each subbundle has a completion report.
- No machine-critical output path relies only on markdown or prompt-only JSON.
- No approval continuation loses structured-output configuration.
- No disabled built-in tool is attached.
- Generic runtime no longer contains calculator-specific hints.
