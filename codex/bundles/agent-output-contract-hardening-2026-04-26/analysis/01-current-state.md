# Current State

## Agent Creation

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.AgentFactory.cs` creates `ChatClientAgent`-backed `AIAgent` instances for OpenAI, Azure OpenAI, and Ollama.
- Runtime creation builds `ChatOptions` with model parameters, instructions, and tools, but it does not set `ResponseFormat`.

## Agent Execution

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.cs` executes streaming runs through `AIAgent.RunStreamingAsync(...)` and assembles updates into `AgentResponse`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` persists the assistant response text, metrics, session state, and run result summary.
- There is no typed run request path for machine-critical outputs.

## Response Parsing

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ExecutionPrompt.cs` tells agents to end with `<!-- PROCESS_STEP_OUTCOME {...} -->`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ToolValidation.cs` parses that markdown comment with regex plus `JsonDocument`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.GovernedOutcomes.cs` maps parsed strings to process statuses and branch outcomes.
- This is safer than free prose, but it is still prompt-only JSON embedded in markdown and is not a typed structured-output contract.

## Tool Registration

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Tools.cs` and `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Tools\MafAgentRuntime.ProcessTools.cs` register typed function tools with `AIFunctionFactory.Create(...)`.
- Tool signatures are mostly typed, but there is no critical-decision finalizer tool.

## Process-State Updates

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Execution.cs` turns parsed response text into `DispatchExecutionOutcome`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Dispatch.cs` persists status, decision summary, and selected branch outcome.
- Current persistence is guarded by tool/artifact checks, but the primary declared outcome is still parsed from assistant text.

## Current Risk

- A malformed, missing, duplicated, or semantically inconsistent comment can drive retries, failures, branch selection, or status updates.
- The system sometimes falls back to implicit completion for governed steps. That is useful operationally, but it must not let markdown or response prose approve workflow transitions.
- Provider support for `ResponseFormat` can vary, so validators and finalizer-tool fallback remain necessary even after structured-output configuration.
