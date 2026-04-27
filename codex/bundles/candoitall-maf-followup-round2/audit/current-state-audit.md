# Current-state audit — latest post-Codex snapshot

## Executive summary

The latest snapshot shows substantial progress:

- Process-step automation now uses `ExecutionInvocationPolicy(FinalizerMode: AgentFinalizerMode.Required, ...)` and passes `ProcessStepOutcomeStructuredOutputContract`.
- `ExecutionInvocationMetadata.Build(...)` normalizes policy into run metadata.
- Required finalizer validation is implemented and exact-one matching finalizer calls are required.
- Required finalizer output replaces `AgentRuntimeResponse.ResponseText` before assistant message persistence.
- Structured output validation now happens before assistant-message creation on both initial and continuation paths.
- Approval continuation resolves and preserves the structured output contract.
- The provider feature matrix now separates structured output, function tools, tool approval, hosted tools, and service-managed history.
- The missing hardening test files from the previous snapshot now exist.
- The default repair service is conservative and extracts a JSON object from wrapped text rather than attempting unsafe semantic mutation.

The remaining gaps are narrower but important. They are mostly boundary and truth-source issues rather than broad architecture failures.

## Positive findings

### P01 — Governed process-step runs now request required finalizer mode

`ProcessRunAutomationDispatchService.Execution.cs` builds a governed invocation policy with:

- `FinalizerMode: AgentFinalizerMode.Required`
- `MaxStructuredOutputRepairAttempts: ExecutionInvocationMetadata.DefaultGovernedRepairAttempts`
- `RequireStructuredOutputValidation: true`

This is a strong improvement. The process step no longer depends only on free-form structured assistant output.

### P02 — Required finalizer output is authoritative before transcript persistence

`AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` calls `ValidateMachineOutputBeforeCompletionAsync(...)` before creating the assistant `ChatMessageRecord`. When required finalizer validation succeeds, `ValidateFinalizerBeforeCompletionAsync(...)` replaces `ResponseText` with serialized finalizer output.

This fixes the previous inconsistency where the persisted transcript could have contained pre-finalizer output.

### P03 — Hardening tests now exist

The repository now contains:

- `AgentFinalizerPolicyTests.cs`
- `AgentOutputContractTests.cs`
- `AgentToolInvocationPolicyTests.cs`
- `ProviderFeatureMatrixTests.cs`
- `AgentRuntimeHardeningStaticRegressionTests.cs`

This is a major improvement from the previous snapshot.

### P04 — Core provider feature matrix is improved

`ProviderServices.cs` now treats OpenAI/Azure OpenAI Responses and Chat Completions as JSON-schema structured-output capable, while tool approval remains limited to Responses transport. This is a better separation of capabilities than the earlier implementation.

### P05 — Repair service is safe as a default

`DefaultAgentOutputRepairService` performs conservative JSON extraction. It does not invent missing business fields or bypass validation. This is acceptable as a safe default, provided it is documented as extraction-only repair rather than semantic repair.

## Findings requiring follow-up

### F01 — Runtime finalizer composition ignores the effective finalizer mode

Severity: Critical

`MafAgentRuntime.AgentFactory.cs` attaches finalizer tools and appends exact-once finalizer instructions whenever a known `structuredOutput` contract is present.

The runtime does not receive the effective `AgentFinalizerMode` that the execution service later resolves from run metadata. This creates a split-brain state:

- Runtime behavior: “finalizer tool is available and must be called exactly once.”
- Execution behavior: mode can still be `Disabled`, `Shadow`, or `Required` depending on run metadata.

This is not just cosmetic. In non-required structured-output flows, the model may be told to call a finalizer that the execution service later ignores. That increases tool-call noise and can destabilize structured output behavior.

Target state:

- Required mode: attach finalizer tool and required finalizer instructions.
- Shadow mode: optionally attach finalizer tool with shadow/telemetry instructions; structured JSON remains source of truth.
- Disabled mode: do not attach finalizer tool or finalizer instructions.

### F02 — Required-finalizer instructions conflict with JSON-schema response format

Severity: High

Current runtime instructions include:

```text
Treat normal assistant text as display-only; workflow state must come from typed machine output.
```

However, the same run also uses `ChatResponseFormat.ForJsonSchema(...)`, which expects the final assistant response to be a schema-conforming JSON object.

Target state for required mode:

```text
Call `<finalizerTool>` exactly once before finishing.
The finalizer arguments are the authoritative machine output.
After the finalizer call, return one JSON object matching the same response schema.
Do not use Markdown or prose.
```

For shadow mode:

```text
The assistant response JSON remains the source of truth.
If you call `<finalizerTool>`, call it at most once and keep its arguments identical in meaning to the final JSON response.
```

### F03 — Tool-policy middleware still misclassifies ordinary tool exceptions as policy blocks

Severity: High

The MAF function-calling middleware throws `InvalidOperationException` for policy-block branches and then catches all `InvalidOperationException` or `NotSupportedException` as policy exceptions.

If an actual tool implementation throws `InvalidOperationException`, the middleware can rewrap it as:

```text
Tool '<name>' was blocked by policy.
```

That corrupts diagnostics and can hide real tool bugs.

Target state:

- Introduce a dedicated `AgentToolPolicyBlockedException`.
- Throw it only from policy branches.
- Catch only that exception as a policy block.
- Let real tool execution exceptions propagate as normal tool/runtime errors.

### F04 — Provider capability truth is still split between core runtime and Workspace UI/DB flags

Severity: High

Core `ProviderFeatureMatrix` correctly marks Ollama as not supporting structured output. But Workspace UI defaults still set:

```text
OllamaProviderAdapter => SupportsStructuredOutput = true
OllamaRemoteProviderAdapter => SupportsStructuredOutput = true
```

Managed SQLite provider bootstrap also persists `SupportsStructuredOutput = false` for OpenAI Chat Completions while the core runtime matrix says Chat Completions supports JSON-schema structured output.

The runtime may be correct, but UI/DB state can mislead operators and tests.

Target state:

- Use `ProviderFeatureMatrix` as the single source of runtime capability truth.
- Make UI labels clear: persisted provider flags are legacy/operator claims, while runtime capabilities are computed.
- Set Ollama structured-output defaults to false unless there is a provider-specific implementation that truly supports MAF `ResponseFormat`/JSON schema reliably.
- Align managed SQLite provider display with actual runtime feature matrix.

### F05 — Hardening test suite is present but too static for several critical invariants

Severity: Medium

Current static tests check source ordering and source strings. They are useful as smoke/regression tests, but they do not fully prove runtime behavior.

Missing behavioral tests include:

- Disabled finalizer mode does not attach finalizer tool or exact-once finalizer instructions.
- Required mode attaches finalizer tool/instructions and fails if finalizer is missing.
- A real tool `InvalidOperationException` is not reported as policy-blocked.
- Workspace UI provider defaults align with the core feature matrix.
- Managed SQLite provider display/runtime capability truth is consistent.

### F06 — No invariant prevents state-changing tools after a finalizer call

Severity: Medium

The finalizer validator captures finalizer calls and enforces exact-one matching finalizer in required mode. It does not currently prove that the finalizer was the last significant tool call.

If the model calls a finalizer and then performs another mutation/validation tool before finishing, the finalizer result might not reflect final state.

This is a recommended improvement rather than a mandatory fix for the current process path.

### F07 — `RunAsync<T>` typed output path has not been evaluated

Severity: Low

The repository currently standardizes on dynamic structured-output contracts and `ResponseFormat`. That is appropriate for process-step automation because contracts are dynamic and must be persisted/replayed.

However, MAF also exposes typed `RunAsync<T>` flows for compile-time known output types. Evaluate whether any simple internal flows could benefit from typed generic output while preserving the existing dynamic contract path for process automation.

## Overall recommendation

Do not start a large workflow rewrite. The current architecture is now close to a reliable MAF-backed process engine. The next round should tighten the boundaries above and add behavior tests that prove the important invariants.
