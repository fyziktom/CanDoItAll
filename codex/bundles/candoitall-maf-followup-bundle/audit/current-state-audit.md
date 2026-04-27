# Current-state audit after second Codex pass

## Executive assessment

The implementation is now much closer to a stable Microsoft Agent Framework runtime. The most important previous gap — process automation missing required finalizer mode — appears solved on the main process dispatch path. `ProcessRunAutomationDispatchService.Execution.cs` now creates an `ExecutionInvocationPolicy` with `FinalizerMode: AgentFinalizerMode.Required`, default governed repair attempts, and required structured-output validation, then passes it through `ExecutionInvocationContext` together with `ProcessStepOutcomeStructuredOutputContract`.

The remaining risks are not about missing big primitives. They are about inconsistent policy propagation, weak regression proof, and a few places where the MAF-native behavior is approximated rather than enforced.

## What is now good

### Structured output contracts

`AgentStructuredOutputContract` rejects unsafe top-level output types such as primitive/string/object/enum/array/IEnumerable/JsonElement/JsonDocument and requires an object DTO. Known contracts now include process step outcome, code review, architecture review, implementation plan, test plan, tool execution decision, process state patch, and human escalation request.

### MAF response format application

`MafAgentRuntime.Session.cs` applies `ChatResponseFormat.ForJsonSchema(...)` through `ChatOptions.ResponseFormat` when `structuredOutput` is present.

### Process automation uses governed policy

`ProcessRunAutomationDispatchService.Execution.cs` now creates:

```csharp
var processInvocationPolicy = new ExecutionInvocationPolicy(
    FinalizerMode: AgentFinalizerMode.Required,
    MaxStructuredOutputRepairAttempts: ExecutionInvocationMetadata.DefaultGovernedRepairAttempts,
    RequireStructuredOutputValidation: true);
```

and passes it with `StructuredOutput: ProcessStepOutcomeStructuredOutputContract`.

### Assistant transcript consistency improved

`AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` validates/finalizes the machine output before creating the persisted assistant `ChatMessageRecord` in both initial and continuation paths.

### Required finalizer validation exists

`ValidateFinalizerBeforeCompletionAsync(...)` validates exact-one finalizer invocation, replaces `ResponseText` with finalizer JSON in required mode, and fails required mode if the finalizer is missing, duplicated, malformed, or invalid.

### Critical DTO finalizer coverage exists

`CreateFinalizerCapture(...)` maps the listed critical contracts to typed finalizer functions created via `AIFunctionFactory.Create(...)`.

### Tool policy is improved

Function-call middleware now blocks `RequireApproval` when no effective approval path is available. This is a major improvement over only logging the requirement.

### Provider matrix is improved

`ProviderProfileService.ResolveFeatureMatrix(...)` now separates function tools from approval requests and recognizes OpenAI/Azure Chat Completions as structured-output capable when using JSON schema response format.

## Remaining critical issues

### C1. Runtime finalizer attachment does not know effective finalizer mode

Evidence:

- `MafAgentRuntime.AgentFactory.cs:67-83` calls `CreateFinalizerCapture(structuredOutput)` and attaches finalizer tools whenever a known structured output is supplied.
- `MafAgentRuntime.AgentFactory.cs:92` appends finalizer instructions whenever that capture exists.
- `AgentFinalizerPolicies.ResolveMode(...)` in `AgentFinalizerPolicy.cs:88-108` resolves enforcement mode from run metadata later, defaulting to `Shadow` for process-step runs and `Disabled` for non-process runs.

This creates a split-brain behavior: the runtime can instruct the model to call a finalizer exactly once, while the execution service later decides that finalizer validation is disabled or only shadow. The source of truth should be one effective policy decided before runtime build.

Impact: confusing prompts, unnecessary tool calls, ignored finalizer calls in disabled mode, and harder debugging.

Target state: pass the effective finalizer mode/policy into `IAgentRuntime` or a new runtime options object. Attach finalizer tools/instructions only when mode is `Required` or `Shadow`. In `Disabled`, do not attach a finalizer tool and do not append finalizer instructions.

### C2. Finalizer instructions conflict with structured response format semantics

Current appended instruction says normal assistant text is display-only. However the same run also configures `ChatOptions.ResponseFormat` with JSON schema. In practice the final assistant response should still be schema-conformant JSON, even if required finalizer arguments are the machine source of truth.

Target state:

- Required mode: "Call the finalizer exactly once. Then return the same schema-conformant JSON object as the final assistant response if the provider requires a final response. The finalizer arguments are authoritative. Do not use Markdown."
- Shadow mode: "Call the finalizer exactly once and return the same schema-conformant JSON object; the execution service will compare them."
- Disabled mode: no finalizer instruction.

### C3. Tool policy exception handling is too broad

Evidence:

- `MafAgentRuntime.AgentFactory.cs:377-380` catches exceptions matching `IsPolicyException(...)` after `next(context, cancellationToken)`.
- `IsPolicyException(...)` returns true for any `InvalidOperationException` or `NotSupportedException`.

A normal tool implementation can throw `InvalidOperationException` or `NotSupportedException`. The middleware will then rethrow it as `Tool '<name>' was blocked by policy`, even when policy allowed the call and the actual tool failed for a business/runtime reason.

Target state: create a dedicated `AgentToolPolicyBlockedException` thrown only by the policy decision branch, and catch only that type for policy-block messages. Let downstream tool exceptions retain their true cause.

### C4. Provider registry still persists stale structured-output capability

Evidence:

- `WorkspaceBackedAgentProviderProfileRegistry.cs:139` sets `entity.SupportsStructuredOutput = model.Transport == ProviderTransportKind.Responses`.
- `ProviderProfileService.ResolveFeatureMatrix(...)` supports JSON schema response format for compatible OpenAI/Azure `Responses` and `ChatCompletions` transports.

Target state: persistence/UI capability flags should come from the same feature-matrix source of truth, not from a stale `Responses` shortcut.

### C5. Provider transport is inferred by display name

Evidence:

- `WorkspaceBackedAgentProviderProfileRegistry.cs:258` maps OpenAI Chat Completions by `IsOpenAiChatCompletionsProvider(provider)`.
- `IsOpenAiChatCompletionsProvider(...)` at `WorkspaceBackedAgentProviderProfileRegistry.cs:303-307` checks if the provider name equals `"OpenAI chat completions"`.

Target state: persist the selected transport in provider metadata/settings and read that metadata first. Keep name-based inference only as a legacy fallback.

### C6. Verification docs claim tests that are not present in the uploaded ZIP

Evidence:

- `docs/agent-runtime-hardening-verification.md:39-41` claims a test filter including `AgentFinalizerPolicyTests`, `AgentToolInvocationPolicyTests`, `ProviderFeatureMatrixTests`, and `AgentRuntimeHardeningStaticRegressionTests` passed 42/42.
- The uploaded ZIP does not contain files matching those class names. Direct ZIP listing found no such test files. The only clearly related unit test file present is `tests/CanDoItAll.Tests.Unit/AgentOutputContractTests.cs`.

Target state: add the missing test classes or correct the verification document. Do not leave documentation claiming tests that cannot be found in the repository.

### C7. Repair service should be named and tested as extraction repair, or upgraded to semantic repair

Evidence:

- `DefaultAgentOutputRepairService` extracts a balanced JSON object from wrapped prose and revalidates it. It does not perform semantic repair using a model or schema-aware transformation.

This is acceptable as a conservative first repair layer. It should be documented and named accordingly, and tests should cover both successful extraction and non-repairable semantic violations.

### C8. Process-step outcome validation is not fully context-aware

Generic `ProcessStepOutcomeValidator` validates shape and coarse consistency. Contextual checks such as "BranchOutcomeKey must be one of this step's available outcomes" happen later in process dispatch. That is reasonable, but the boundary should be explicit and tested:

- generic contract validation must fail malformed DTOs;
- process-context validation must fail invalid branch selection, missing required branch selection, and evidence rules for completion;
- completion must never fall back to Markdown or text heuristics for governed runs.

### C9. Workspace-backed tool approval composition should fail fast for unusable mutation tools

The middleware blocks mutation calls when no effective approval path exists, but tool composition can still attach mutation tools that are known to be unusable for the current provider/run unless auto-approval is enabled. This is safer than execution, but worse than failing at composition time with a clear diagnostic.

Target state: when a mutation/destructive tool would require approval and no approval wrapper/provider/application approval path is available, either do not attach it or fail runtime build before the model sees it.

### C10. MAF Workflow usage is still mostly checkpoint-store bridging, not typed workflow orchestration

The repo references `Microsoft.Agents.AI.Workflows` and uses `FileSystemJsonCheckpointStore` to bridge pending approvals. That is useful, but the multi-agent process itself is still mostly the custom process dispatcher. This is not necessarily wrong; just do not present it as full MAF workflow orchestration. Add a roadmap or adapter plan for selected process subflows.
