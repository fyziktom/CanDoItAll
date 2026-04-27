# Post-Codex MAF Stabilization Audit

## Executive assessment

The implementation moved in the right direction and includes meaningful hardening. It should not be accepted as complete yet.

The repository now has real structured-output machinery and several runtime gates. This is a strong improvement over prompt-only JSON. The remaining issues are mostly in enforcement boundaries: finalizers are shadow-only in the main process path, repair/retry is missing, approval semantics are not fully aligned with provider capabilities, and validation has null-safety gaps.

## Implemented correctly or mostly correctly

### 1. Structured-output contracts

`AgentStructuredOutputContract` rejects unsupported weak top-level output types such as primitives, enums, strings, arrays, `IEnumerable<>`, `object`, `JsonElement`, and `JsonDocument`. This aligns with the MAF `ResponseFormat` requirement to use object DTOs rather than top-level primitives/arrays.

Evidence:

- `src/CanDoItAll.AgentFramework.Models/OutputContracts/AgentOutputContracts.cs`
- `AgentStructuredOutputContract` top-level object validation.
- `AgentStructuredOutputContracts.ProcessStepOutcomeResult` registered.

### 2. MAF `ResponseFormat` is applied at runtime

`MafAgentRuntime.Session.cs` applies `ChatResponseFormat.ForJsonSchema(...)` when a structured-output contract is present.

Evidence:

- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs`
- `CreateRunOptions(...)`
- `ApplyStructuredResponseFormat(...)`

### 3. Approval continuations now preserve structured-output contracts

The prior `structuredOutput: null` continuation gap appears fixed. `ResolveContinuationStructuredOutputContract(run)` is used and passed into `RespondToPendingApprovalsAsync` and `ContinueAutoApprovedRunAsync`.

Evidence:

- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:83`
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:134-145`
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:153-164`

### 4. Completion-time validation exists

Before completion, structured output is deserialized and validated. For governed process-step runs, a missing validator throws.

Evidence:

- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:857-917`

### 5. Shadow finalizer exists

A typed finalizer capture for `ProcessStepOutcomeResult` is attached through `AIFunctionFactory.Create(...)`.

Evidence:

- `src/CanDoItAll.AgentFramework.Core/Finalizers/AgentFinalizerPolicy.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:401-413`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:730-755`

### 6. Function-call middleware/tool policy exists

The MAF agent builder now has middleware around function invocations, logs tool-policy decisions, and blocks deny/skip decisions.

Evidence:

- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:292-380`
- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`

### 7. Built-in tools are no longer always enabled

`IsBuiltInToolEnabled(...)` now respects `configuration.Enabled != false`.

Evidence:

- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`

## Critical gaps

### C1. Required finalizer is not enabled for the main process path

The main process automation path creates `ExecutionInvocationContext` with `MetadataJson: "{}"`. `AgentFinalizerPolicies.ResolveMode(...)` defaults governed process-step structured output to `Shadow`, not `Required`.

This means the finalizer is advisory/logging-only for process runs unless some other caller explicitly sets metadata. The previous requirement was a true exact-once finalizer for critical workflow decisions; current process-step automation does not enforce that.

Evidence:

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:115-128`
- `src/CanDoItAll.AgentFramework.Core/Finalizers/AgentFinalizerPolicy.cs:61-81`

Required fix:

- Add an explicit finalizer mode to the invocation context or metadata for critical process-step runs.
- Use `required` for process automation by default once shadow telemetry is deemed safe, or make it configurable by process/agent/step policy.
- Add tests proving missing finalizer tool call fails in required mode on the process automation path.

### C2. Assistant transcript can diverge from finalized output

The execution code creates `ChatMessageRecord` using `runtimeResponse.ResponseText` before `ValidateMachineOutputBeforeCompletionAsync(...)`. In required finalizer mode, validation can replace `runtimeResponse.ResponseText` with the finalizer JSON, but the already-created assistant message still contains the pre-finalizer text.

Evidence:

- Initial run path: `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:617-630`
- Continuation path: `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:173-186`
- Required finalizer output replacement: `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:985-1000`

Required fix:

- Validate/finalize before creating the assistant message; or update assistant message content after finalization.
- Add regression tests for required-finalizer output replacing the stored assistant message content.

### C3. Output repair/retry is not implemented

The models exist (`AgentOutputRepairRequest`, `AgentOutputRepairResult`, repair-related DTOs), but invalid structured output throws immediately. There is no bounded repair call, no retry count, and no re-validation loop.

Evidence:

- `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidation.cs`
- `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:888-903`

Required fix:

- Add a concrete `IAgentOutputRepairService` or equivalent repair orchestrator.
- Default maximum attempts: 1 for governed process automation; configurable up to 2.
- Repair output must be re-validated and must not bypass finalizer/policy/security gates.
- Store original raw output hash, repaired raw output hash, and repair attempt count.

### C4. Provider capability matrix is inconsistent with MAF docs

`ProviderProfileService.ResolveFeatureMatrix(...)` currently sets structured output support only for OpenAI/Azure OpenAI Responses transport. MAF documentation says `ResponseFormat` can be configured on `AgentRunOptions`, and the examples include Azure OpenAI Chat Completion service with `ChatResponseFormat.ForJsonSchema<T>()` when the underlying chat client supports it. The current blanket matrix can incorrectly reject valid Chat Completion structured-output scenarios.

The same method sets `SupportsToolApprovalWrappers = normalizedProvider.SupportsTools`, but MAF's tools overview matrix distinguishes Function Tools from Tool Approval support. Tool Approval is not universally equivalent to ordinary function-tool support.

Evidence:

- `src/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs:136-168`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:382-399`
- `tests/CanDoItAll.Tests.Unit/ProviderFeatureMatrixTests.cs`

Required fix:

- Split ordinary function-tool support from approval-request support.
- Model support by client/transport/provider combination, not only provider kind.
- Add tests aligned with MAF docs: compatible chat clients may support structured output; approval support is narrower than function tools.

### C5. `RequireApproval` policy is not enforced by middleware unless wrapper exists and works

The function-call middleware blocks only `Deny` and `SkipExecution`. If the policy returns `RequireApproval`, the code proceeds to `next(...)`. That is safe only if the tool is actually wrapped in `ApprovalRequiredAIFunction` and the selected provider/client supports approval requests.

Evidence:

- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:314-375`
- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`

Required fix:

- In middleware, if `RequireApproval` and no effective approval mechanism exists, block or convert to an application-level pending approval before executing the tool.
- Provider capability matrix must tell whether approval wrappers are effective for that client/transport.
- Add tests where a mutation tool returns `RequireApproval` but no wrapper/provider support is available; execution must not happen.

### C6. Validators are not null-safe

Some validators call `.Count` on required collections without null-checks. `System.Text.Json` plus C# `required` does not guarantee every invalid/missing field becomes a clean validation error in every scenario. The validator layer must treat malformed/missing collections as validation failures, not runtime exceptions.

Evidence:

- `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidatorRegistry.cs:172-189`
- `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidatorRegistry.cs:199-227`
- `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidatorRegistry.cs:230-262`
- `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidatorRegistry.cs:265-282`
- `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidation.cs:72-75`

Required fix:

- Null-check every collection and nested object before access.
- Catch validator exceptions in `DeserializeAndValidate` and turn them into `agent.output.validator_exception` validation errors.
- Add tests for missing and explicit-null collections.

### C7. Finalizer support covers only `ProcessStepOutcomeResult`

The finalizer registry only resolves a finalizer for the process-step outcome. Other critical contracts such as `CodeReviewResult`, `ArchitectureReviewResult`, `ImplementationPlanResult`, `TestPlanResult`, and `ToolExecutionDecisionResult` remain structured-output-only.

Required fix:

- Add finalizer policies/tools for critical decision DTOs or explicitly document why a given DTO does not require a finalizer.
- Add tests for exact-one finalizer behavior per critical contract.

## Important medium-priority gaps

### M1. Finalizer instructions still describe shadow mode

`AppendFinalizerInstructions(...)` says to call the tool exactly once with the same decision returned as structured output. In required mode, the finalizer should be the machine source of truth, and normal assistant text should not be treated as the machine result.

Evidence:

- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:416-430`

Required fix:

- Generate different instructions for shadow and required mode.
- Required mode: tool call is source of truth; direct assistant text is display-only and must not be used as machine output.
- Shadow mode: call the tool with the same object as structured output for telemetry comparison.

### M2. Contract registry is too narrow

`AgentStructuredOutputContracts.KnownContracts` currently appears to register `ProcessStepOutcomeResult`. If other critical DTOs will be used across approval continuations or restored runs, they need resolvable contract keys.

Required fix:

- Register all stable contract keys.
- Persist contract key, schema name, schema description, and output type identity safely across continuations.
- Add tests that continuations resolve every known contract used by execution runs.

### M3. Tool classification is too name-based

Tool policy relies heavily on tool-name prefixes/classification. This is brittle with hosted tools, MCP tools, provider-native tools, and renamed functions.

Required fix:

- Add metadata classification at tool composition time.
- Carry classification into policy context explicitly.
- Reject unknown mutation/destructive tools by default.

### M4. Calculator recovery is still tied to process automation

Calculator-specific guidance moved out of MAF runtime, which is good, but it still lives in the process automation dispatch layer. For long-term stability, domain-specific recovery guidance should be configured per process template/skill, not hardcoded in generic process automation.

## Build/test status

This audit environment does not have the .NET SDK installed:

```text
dotnet: command not found
```

The repository has `global.json` requesting SDK `10.0.200`. Codex must run and attach outputs for:

```bash
dotnet --info
dotnet restore CanDoItAll.sln
dotnet build CanDoItAll.sln --configuration Release --no-restore
dotnet test CanDoItAll.sln --configuration Release --no-build
```

If the actual solution file name differs, Codex must use the repository's real solution or test entry point and report the command used.
