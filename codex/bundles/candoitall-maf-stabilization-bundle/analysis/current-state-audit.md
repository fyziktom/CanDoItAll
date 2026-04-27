# Current-State Audit

This audit is based on the uploaded repository snapshot. It focuses on how the project currently uses Microsoft Agent Framework and where MAF-native capabilities can stabilize behavior.

## What is already strong

### Native MAF package usage

Relevant project files show direct use of MAF 1.0 packages:

- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
  - `Microsoft.Agents.AI` 1.0.0
  - `Microsoft.Agents.AI.OpenAI` 1.0.0
  - `Microsoft.Agents.AI.Mem0` preview
  - `Azure.AI.OpenAI`
  - `ModelContextProtocol`
  - `OllamaSharp`
- `src/CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
  - `Microsoft.Agents.AI.Workflows` 1.0.0

### Structured-output foundation exists

The project now has a real typed structured-output foundation:

- `src/CanDoItAll.AgentFramework.Models/OutputContracts/AgentOutputContracts.cs`
  - DTOs and enums for agent outputs.
  - Top-level structured-output guard through `AgentStructuredOutputContract`.
  - Rejects primitive, enum, string, array, enumerable, `object`, `JsonElement`, and `JsonDocument` as top-level contracts.
- `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidation.cs`
  - Strict JSON serializer options.
  - String enum converter with integer enum values disallowed.
  - `DeserializeAndValidate<TOutput>()` pipeline.
  - Raw output hash calculation.

### MAF `ResponseFormat` is applied

`src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs` applies structured response format through:

- `ApplyStructuredResponseFormat(...)`
- `ChatResponseFormat.ForJsonSchema(...)`

Relevant evidence:

- `MafAgentRuntime.Session.cs:238` calls `ApplyStructuredResponseFormat(chatOptions, structuredOutput)`.
- `MafAgentRuntime.Session.cs:247-261` configures `chatOptions.ResponseFormat` with a JSON schema for the structured output type.

### Process-step output is now structured

`src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.OutputValidation.cs` defines:

- `ProcessStepOutcomeStructuredOutputContract`
- `TryReadProcessStepOutcome(...)`
- nested `ProcessStepOutcomeValidator`

`src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:128` passes `StructuredOutput: ProcessStepOutcomeStructuredOutputContract` when executing process steps.

`src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs` instructs agents to produce `ProcessStepOutcomeResult` through configured structured output and states that status is the only workflow source of truth.

### Tool approval and MCP validation are substantial

The runtime already uses MAF function tools and approval wrappers:

- `AIFunctionFactory.Create(...)` is used for workspace/plugin/project/process tools.
- `ApprovalRequiredAIFunction` is applied to write/mutation tools, skill scripts, local MCP tools, and some configured tools.
- Local MCP configuration validates allow lists, command policy, and secrets handling.
- Hosted MCP support is separated from local MCP.

### Session and checkpoint infrastructure exist

The runtime has:

- MAF `AgentSession` serialization/deserialization.
- Runtime session keys.
- Pending approval cache.
- `WorkflowBackedAgentExecutionCheckpointBridge` using MAF workflow checkpointing for pending-approval checkpoints.

### The process layer already does real governance

The process dispatcher already checks:

- Required tool execution receipts.
- Critical tool failures.
- Missing concrete implementation proof.
- Missing browser proof.
- Missing required artifacts.
- Branch outcome selection.
- Provider fallback and retry conditions.

This is a strong sign that the system should be hardened, not replaced.

## Main gaps and risks

### Gap 1: Structured output is not preserved through all continuation paths

Initial process runs pass `StructuredOutput: ProcessStepOutcomeStructuredOutputContract`, but continuation paths in `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` currently call the runtime with `structuredOutput: null`.

Evidence:

- `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:142` passes `structuredOutput: null`.
- `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:160` passes `structuredOutput: null`.

Impact:

- If the model performs final reasoning after a manual approval, it may no longer be constrained by the structured-output schema.
- The process dispatcher can detect invalid output, but the constraint should not be dropped in the first place.

Recommended fix:

- Persist or resolve the expected structured-output contract for pending runs and continuations.
- Pass it to `RespondToPendingApprovalsAsync(...)` and auto-approved continuation paths.
- Fail governed steps if the contract is missing and cannot be reconstructed.

### Gap 2: Tool governance should be MAF-native and pre-execution

The runtime has approval wrappers and a repeated-tool guard, but central policy is not yet expressed as a MAF function invocation middleware.

Evidence:

- `MafAgentRuntime.AgentFactory.cs:278` uses `agent.AsBuilder()`.
- `MafAgentRuntime.AgentFactory.cs:276-311` instruments function calls and telemetry, but policy enforcement is not centralized there.
- `MafAgentRuntime.cs:563-665` performs repeated tool-call detection after streamed updates are received.

Impact:

- Some bad tool behavior is detected only after it has happened or after the response stream has already advanced.
- The code path for policy is scattered between capability creation, approval wrappers, MCP validation, repeated-call guard, and dispatcher recovery.

Recommended fix:

- Add a dedicated MAF function invocation middleware layer that can inspect tool names/arguments, apply allow/deny/approval/sanitization rules, and emit policy telemetry before execution.
- Keep approval wrappers, but treat middleware as the central guardrail plane.

### Gap 3: Generic execution completion can still be text-first

Process-step execution validates `ProcessStepOutcomeResult`, but generic `ExecutionRun` completion can mark the run completed/succeeded when the runtime returns no pending approvals.

Evidence:

- `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` persists runtime responses and completion state.
- Structured output is part of `ExecutionRunRequest`, but validation is not uniformly enforced before all machine-critical completions.

Impact:

- Non-process agent runs can still be used by future workflow features without the same guarantees.

Recommended fix:

- Add a central typed runner/contract validation layer at execution-service level.
- If `StructuredOutput` is present or the source context declares machine-critical output, completion must require typed validation.

### Gap 4: Validators are too narrow

The DTO catalog is broad, but validators are not equally broad.

Evidence:

- `AgentOutputValidation.cs` contains `ProcessStatePatchValidator` as the main concrete reusable validator.
- `ProcessStepOutcomeValidator` is nested in the process dispatcher and only checks reason, failed next actions, and one completed-next-action inconsistency.

Impact:

- DTO shape is enforced, but semantic correctness is not consistently enforced.

Recommended fix:

- Add validators and a registry for each machine-critical DTO family.
- Promote process-step outcome validation into a reusable validator.
- Validate branch selection, evidence requirements, status/next-action consistency, and governed-step rules.

### Gap 5: Finalizer tools are documented but not implemented

`docs/agent-output-contracts.md` documents the finalizer tool pattern, but no implementation was found for critical decision finalizers.

Impact:

- Structured final responses are much better than prompt-only JSON, but exact-once tool finalization is stronger for high-risk decisions.

Recommended fix:

- Implement finalizer tools for side-effectful or critical decisions.
- Start with process outcome finalization in shadow mode or with process patch/deployment/security decisions.

### Gap 6: MAF workflows are not yet the process boundary

The project references `Microsoft.Agents.AI.Workflows` and uses `FileSystemJsonCheckpointStore` for approval checkpoints, but process orchestration remains custom.

Impact:

- This is acceptable short-term, because the process engine has domain-specific governance.
- However, MAF workflows/orchestrations can improve recoverability, typed routing, checkpointing, and multi-agent pattern clarity.

Recommended fix:

- Add a small adapter/harness, not a rewrite.
- Use MAF workflows for selected agent subflows: sequential, concurrent reviews, handoff, and checkpointed long-running step execution.

### Gap 7: Tool enabled configuration is ignored for built-in tools

Evidence:

- `MafAgentRuntime.Capabilities.Tools.cs:214-215` returns `true` from `IsBuiltInToolEnabled(...)` regardless of configuration.

Impact:

- Configured disabled tools can still be attached.

Recommended fix:

- Honor `BuiltInToolConfiguration.Enabled` and add tests.

### Gap 8: Generic runtime contains calculator-specific recovery hints

Evidence:

- `MafAgentRuntime.cs:633-665` includes hard-coded strings beginning with “If this is the calculator process...”.

Impact:

- The generic runtime becomes scenario-specific.
- Other processes may receive irrelevant recovery instructions.

Recommended fix:

- Move domain-specific recovery guidance into process template, skill, scenario harness, or a pluggable recovery directive provider.

### Gap 9: Provider capabilities need a single source of truth

Evidence:

- `WorkspaceBackedAgentProviderProfileRegistry.cs:139` sets structured-output support based on `model.Transport == ProviderTransportKind.Responses`.
- `RuntimeHostServiceCollectionExtensions.cs` creates a managed SQLite OpenAI provider with `SupportsStructuredOutput = false` and later forces it false if true.

Impact:

- Capability semantics can diverge between UI/provider registry/runtime.
- Runtime may attach unsupported features or fail later than necessary.

Recommended fix:

- Create a central provider capability matrix service and use it for provider creation, validation, runtime gating, health checks, and UI display.

### Gap 10: Build/test verification was not possible in this environment

Attempted command:

```bash
dotnet build CanDoItAll.slnx --no-restore -v:minimal
```

Result:

```text
bash: dotnet: command not found
```

Codex must run build/test validation in the actual repository environment.
