# Evidence map

This map lists the most important repository locations inspected for the post-second-pass audit.

## Structured output and DTO contracts

- `src/CanDoItAll.AgentFramework.Models/OutputContracts/AgentOutputContracts.cs`
  - `AgentStructuredOutputContract` rejects unsafe top-level output types.
  - `AgentStructuredOutputContracts` registers known contract keys for process outcome, review, plan, tool decision, process patch, and escalation DTOs.
  - `ProcessStepOutcomeResult`, `CodeReviewResult`, `ImplementationPlanResult`, `ArchitectureReviewResult`, `TestPlanResult`, `ToolExecutionDecisionResult`, `ProcessStatePatch`, `HumanEscalationRequest` are present.

## Structured output validation and repair

- `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidation.cs`
  - `AgentOutputJson.DeserializeAndValidate(...)` deserializes with web JSON defaults and validates with registered validators.
  - `DefaultAgentOutputRepairService` attempts conservative JSON object extraction.
- `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidatorRegistry.cs`
  - Validators are present for all known critical DTOs.

## Finalizer policy and validation

- `src/CanDoItAll.AgentFramework.Core/Finalizers/AgentFinalizerPolicy.cs`
  - `AgentFinalizerPolicies.TryResolveForStructuredOutput(...)` maps known structured output contracts to required finalizer policies.
  - `ResolveMode(...)` still defaults process-step runs to `Shadow` and non-process runs to `Disabled` when metadata does not explicitly set the mode.
  - `DefaultAgentFinalizerValidator` validates exactly one matching finalizer invocation and validates its arguments.

## Runtime finalizer attachment

- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
  - `CreateRuntimeBuildAsync(...)` attaches finalizer tools and instructions based only on `structuredOutput`.
  - `CreateFinalizerCapture(...)` maps critical contracts to typed functions using `AIFunctionFactory.Create(...)`.
  - `AppendFinalizerInstructions(...)` always appends exact-once instructions when a finalizer capture exists.

## Execution run validation and transcript order

- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
  - Initial and continuation paths call `ValidateMachineOutputBeforeCompletionAsync(...)` before persisting assistant messages.
  - `ValidateFinalizerBeforeCompletionAsync(...)` replaces response text with finalizer output in required mode.
  - `ResolveContinuationStructuredOutputContract(...)` restores structured output for pending approval continuations.

## Process automation dispatch

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
  - Main process execution now uses `ExecutionInvocationPolicy(FinalizerMode: Required, MaxStructuredOutputRepairAttempts: DefaultGovernedRepairAttempts, RequireStructuredOutputValidation: true)`.
  - It passes `StructuredOutput: ProcessStepOutcomeStructuredOutputContract`.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.GovernedRules.cs`
  - `CanImplicitlyCompleteGovernedStep(...)` returns false, which is good for preventing governed runs from succeeding without a valid process outcome.
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.GovernedOutcomes.cs`
  - Branch outcome validation is process-context-specific and should be tested as such.

## Tool policy and function middleware

- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
  - Default policy classifies known mutation and validation tools, blocks unknown tools, limits repeated mutation/validation signatures, and requires approval for mutation tools unless auto-approved.
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
  - Function-call middleware evaluates the policy before `next(context, cancellationToken)`.
  - It blocks `RequireApproval` without an effective approval path.
  - It still catches generic `InvalidOperationException`/`NotSupportedException` as policy exceptions.

## Provider feature matrix

- `src/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs`
  - `ResolveFeatureMatrix(...)` now separates function-tool support, structured-output support, JSON schema response format support, and approval-request support.
- `src/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs`
  - `SaveProviderAsync(...)` still persists `SupportsStructuredOutput = model.Transport == ProviderTransportKind.Responses`.
  - Mapping transport still relies on provider display name for the OpenAI Chat Completions profile.

## Verification docs/tests mismatch

- `docs/agent-runtime-hardening-verification.md`
  - Claims hardening-specific test classes passed.
- Uploaded ZIP listing
  - Does not contain `AgentFinalizerPolicyTests.cs`, `AgentToolInvocationPolicyTests.cs`, `ProviderFeatureMatrixTests.cs`, or `AgentRuntimeHardeningStaticRegressionTests.cs`.
  - Contains `tests/CanDoItAll.Tests.Unit/AgentOutputContractTests.cs` only among obviously related unit files.

## MAF workflow/checkpoint bridge

- `src/CanDoItAll.AgentFramework.Core/Execution/ExecutionCheckpointServices.cs`
  - Uses `Microsoft.Agents.AI.Workflows.Checkpointing.FileSystemJsonCheckpointStore` for pending approval checkpoint payloads.
  - This is a useful checkpoint bridge but not full workflow orchestration.
