# Evidence Map

This map is based on the uploaded round 2 repository snapshot.

## Process retry loop

`src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`

- lines 41-44: `successfulToolNamesAcrossAttempts` initialized and seeded with artifact-inspection grounding.
- lines 47-50: retry loop bounded by `maxExecutionAttempts`.
- lines 58-71: if a recoverable execution run exists, the code can adopt its execution run and chat session id.
- lines 90-103: creates/reuses a chat session and sets `FinalizerMode.Required` in `ExecutionInvocationPolicy`.
- lines 107-135: calls `ExecuteRunAsync(...)` with `SourceKind: "process-step"`, process metadata, invocation policy, `AutoApprovePendingToolCalls: true`, and `ProcessStepOutcomeStructuredOutputContract`.
- lines 137-155: failed agent run is caught and then inspected.
- lines 220-235: resolves successful tools, missing required tools, unresolved critical failures, and completion status.
- lines 263-292: provider repair can switch assigned agents to fallback providers and resets `automationChatSessionId`.
- lines 335-345: regular retry resets `automationChatSessionId = null` and builds a text recovery directive.
- lines 352-356: max attempts are 3 by default and 5 for concrete implementation proof steps.

## Recovery directive

`src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryDirective.cs`

- line 25: `BuildRecoveryDirective(...)` returns a string.
- lines 40-56: missing tools and critical failures are rendered into text.
- lines 90-92: tells agent not to stop after inspection/planning.
- lines 97-147: implementation retry guidance is detailed and domain/project-structure-specific.
- lines 180-197: browser proof retry guidance.
- lines 230-239: instructs the agent to return valid `ProcessStepOutcomeResult`.
- lines 241-248: previous response summary is truncated to about 400 characters.

## Tool carry-forward

`src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`

- lines 55-93: `ResolveMissingRequiredToolExecutionsWithCarryForward(...)`.
- lines 66-74: prior successful tool names are considered if `ShouldCarryForwardSuccessfulToolName(...)` allows them.
- lines 132-152: current-attempt-only proof tools are not carried forward for implementation/browser proof requirements.

## Manual rerun

`src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Rerun.cs`

- lines 9-126: `RerunAgentStepAsync(...)` allows manual rerun for blocked/failed agent-owned steps.
- lines 158-215: `BuildManualRerunDirective(...)` returns text: fresh attempt, preserve previous artifacts, include operator reason and blocked reason.

## MAF session behavior

`src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs`

- lines 11-26: restore serialized session if compatible; otherwise create a new session.
- lines 28-55: if serialized session is restored, send only the new prompt; otherwise replay transcript messages.
- lines 223-261: `ApplyStructuredResponseFormat(...)` sets `ChatResponseFormat.ForJsonSchema(...)`.
- lines 264-294: service-managed history is not reused with an incompatible provider.
- lines 296-299: `ShouldReplayTranscriptAfterApproval(...)` currently returns false.

`src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`

- lines 718-790: failed execution runs clear session compatibility and throw `AgentChatRunFailedException` if a session exists.
- lines 876-1015: structured output validation, required finalizer validation, bounded JSON extraction repair, revalidation, and failure.
- lines 1130-1193: finalizer sequence validation requires finalizer to be last significant tool for governed required runs.

## Tool policy and process tool classification gap

`src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`

- lines 195-224: tool classification.
- lines 227-237: mutation classification only includes workspace mutation tools.
- lines 240-245: validation classification only includes dotnet restore/build/test/run.

`src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs`

- lines 63-82: process definition save/publish/delete/import tools are registered.
- lines 95-110: process run start, step transition, assignment resolution, and artifact record tools are registered.
- lines 182-243 and 315-380: these tool implementations enforce write access and mutate process data.

`src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`

- lines 184-205: internal process tools are attached directly, not wrapped with approval.

## Provider approval capability gap

`src/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs`

- lines 136-181: feature matrix supports structured output for OpenAI/Azure Responses or Chat Completions, but approval support only for Responses.

`tests/CanDoItAll.Tests.Unit/ProviderFeatureMatrixTests.cs`

- tests assert OpenAI/Azure Chat Completions structured output support but no approval support. This should be verified against the installed MAF package and official docs.

## Secret finding

`src/CanDoItAll.Web/appsettings.json`

- contains an OpenAI API key-like value. Do not copy it. Remove and rotate.
