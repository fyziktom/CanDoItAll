# Repository Evidence Map

This map lists the most important files and code locations observed in the uploaded post-Codex snapshot.

## Structured output contracts

- `src/CanDoItAll.AgentFramework.Models/OutputContracts/AgentOutputContracts.cs`
  - Defines DTOs such as `AgentStepResult<TPayload>`, `AgentOutputEnvelope<TPayload>`, `ProcessStatePatch`, `CodeReviewResult`, `ImplementationPlanResult`, `ArchitectureReviewResult`, `TestPlanResult`, `ToolExecutionDecisionResult`, and `ProcessStepOutcomeResult`.
  - `AgentStructuredOutputContract` rejects weak top-level output types.
  - `AgentStructuredOutputContracts.ProcessStepOutcomeResult` is a known contract.

## Structured output runtime application

- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs`
  - `CreateRunOptions(...)`
  - `ApplyStructuredResponseFormat(...)`
  - Applies `ChatResponseFormat.ForJsonSchema(...)`.

## Structured output process execution

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:102-129`
  - Process automation calls `ExecuteRunAsync(...)` with `StructuredOutput: ProcessStepOutcomeStructuredOutputContract`.
  - It still passes `MetadataJson: "{}"`, so finalizer mode is not required by default.

## Approval continuation structured output

- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:83`
  - Resolves continuation contract.
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:134-145`
  - Passes `structuredOutput` to approval continuation.
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:153-164`
  - Passes `structuredOutput` to auto-approved continuation.

## Completion validation and finalizer validation

- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:857-917`
  - Validates structured output before completion.
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:919-1023`
  - Validates finalizer invocations and replaces response text in required mode.

## Assistant-message ordering bug

- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:617-630`
  - Creates assistant message before validation/finalizer on initial run path.
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:173-186`
  - Creates assistant message before validation/finalizer on continuation path.
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:985-1000`
  - Required finalizer can replace response text after assistant message was already created.

## Finalizer policies/tools

- `src/CanDoItAll.AgentFramework.Core/Finalizers/AgentFinalizerPolicy.cs`
  - Finalizer modes: Disabled, Shadow, Required.
  - Process-step default is Shadow when metadata is absent.
  - Required mode is available only through metadata.
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:401-413`
  - Attaches `submit_process_step_outcome` using `AIFunctionFactory.Create(...)`.
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:416-430`
  - Finalizer instructions currently ask for both structured output and a matching tool call.

## Tool policy/middleware

- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:292-380`
  - Function-call middleware evaluates policy and logs decisions.
  - Blocks only `Deny` and `SkipExecution`, not `RequireApproval`.
- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
  - Defines decisions and metadata classification.

## Provider capability matrix

- `src/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs:136-168`
  - Structured output support limited to OpenAI/Azure Responses transport.
  - Approval wrappers set equal to `SupportsTools`.
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:382-399`
  - Runtime rejects structured output when matrix says unsupported.

## Validators

- `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidation.cs:25-76`
  - Deserializes and calls validator.
  - Does not catch validator exceptions.
- `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidatorRegistry.cs:172-282`
  - Several validators access required collection `.Count` without null checks.

## Tests added by Codex

- `tests/CanDoItAll.Tests.Unit/AgentOutputContractTests.cs`
- `tests/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs`
- `tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`
- `tests/CanDoItAll.Tests.Unit/ProviderFeatureMatrixTests.cs`

These tests are useful but currently encode some questionable provider capability assumptions and do not cover the assistant-message finalizer divergence bug or repair/retry.
