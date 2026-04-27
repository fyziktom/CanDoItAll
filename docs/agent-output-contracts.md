# Agent output contracts

Prompt-only JSON is not an acceptable machine contract for CanDoItAll workflow automation. Agent text can be useful for human display, but process transitions, branch selection, approval decisions, tool decisions, and state patches must come from typed DTO fields that are deserialized and validated before persistence.

## Structured output

Execution runs that need a machine-readable result should set `ExecutionRunRequest.StructuredOutput` to an `AgentStructuredOutputContract`:

```csharp
StructuredOutput: AgentStructuredOutputContract.For<ProcessStepOutcomeResult>(
    "process_step_outcome_result",
    "Validated machine contract for process step completion, branch selection, next actions, and display-only markdown summary.")
```

The Microsoft Agent Framework runtime maps that contract to:

```csharp
chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(
    structuredOutput.OutputType,
    AgentOutputJson.SerializerOptions,
    structuredOutput.SchemaName,
    structuredOutput.SchemaDescription);
```

Top-level outputs must be object DTOs. `AgentStructuredOutputContract` rejects primitive, string, enum, array, collection, `object`, `JsonElement`, and `JsonDocument` contracts. If the result is logically a list, wrap it:

```csharp
public sealed class ImplementationPlanResult
{
    public required IReadOnlyList<ImplementationTask> Tasks { get; init; }
    public required IReadOnlyList<string> Risks { get; init; }
    public required IReadOnlyList<string> EvidenceRefs { get; init; }
    public string? HumanReadableSummaryMarkdown { get; init; }
}
```

## Current process-step contract

Governed process automation uses `ProcessStepOutcomeResult`.

Valid example:

```json
{
  "status": "Completed",
  "reason": "Build, tests, browser proof, and required artifacts were completed.",
  "branchOutcomeKey": "approved",
  "evidenceRefs": ["artifact://process/implementation-change-set.md"],
  "nextActions": [],
  "humanReadableSummaryMarkdown": "## QA proof\nBrowser screenshot and console evidence were captured."
}
```

Invalid example:

```text
Review complete. <!-- PROCESS_STEP_OUTCOME {"status":"Completed"} -->
```

The invalid example is markdown text with embedded JSON. It is rejected before it can drive workflow state.

## Validation pipeline

Machine-critical output flows through this sequence:

1. Run the agent with a structured output contract when the provider supports it.
2. Capture the raw response text for observability.
3. Deserialize with `AgentOutputJson.SerializerOptions`.
4. Validate with an `IAgentOutputValidator<TOutput>`.
5. Use typed fields for workflow decisions.
6. Persist only validated outcome data into process state.
7. Emit process events only after the validated outcome changes state.
8. Retry or fail/escalate when output is missing or invalid.

`AgentOutputJson` uses strict JSON settings: no comments, no trailing commas, and string enums. Raw output hashes are available for diagnostics without logging sensitive full payloads.

The execution service now preserves structured-output metadata on `ExecutionRunRecord` and workflow checkpoints. Approval continuations restore the original contract before resuming the provider call, and governed process-step runs fail if the stored contract cannot be resolved.

## Business rules

Validators must enforce contract-specific rules, not just JSON shape. Existing examples:

- `ProcessStepOutcomeResult.Reason` is required.
- Failed process outcomes must include a next action.
- Completed process outcomes must not ask for follow-up input as a next action.
- `ProcessStatePatch` operations must use allowed JSON-pointer paths.
- Process patches cannot mutate protected paths.
- Add/replace patch operations require a value and every operation requires a reason.

Do not read workflow approval, failure, branch, or transition decisions from `HumanReadableSummaryMarkdown`.

## Repair and retry

Invalid output is not accepted silently. A retry prompt should include only:

- the validation errors,
- the invalid raw output or its redacted form,
- the target contract name and schema expectation,
- the instruction to return only the target structured output.

Retries must be bounded. After the retry limit, return a typed failure or human escalation request. Repaired output must go through the same deserialization, validation, and policy checks as first-pass output.

## Finalizer tools

Use a finalizer function tool for critical decisions that should be committed exactly once, such as deployment approval, security-sensitive tool decisions, architecture approval, or process-state patch submission. Register finalizer functions with typed signatures through `AIFunctionFactory.Create(...)`.

The agent instruction should say that the finalizer tool must be called exactly once and that normal assistant text is display-only. Missing finalizer calls and malformed tool arguments must be treated as invalid output.

`AgentFinalizerPolicy` and `DefaultAgentFinalizerValidator` enforce the exact-once invariant. Required finalizers fail when the expected tool is missing, called multiple times, has malformed arguments, or lacks a registered output validator. Assistant text is ignored when a finalizer is required.

Process-step runs that use `ProcessStepOutcomeResult` now attach `submit_process_step_outcome` as a typed shadow finalizer. The execution service logs whether the finalizer was observed, validates captured arguments through the same validator registry, and compares valid finalizer output to the structured response. Structured output remains the default process-step source of truth in shadow mode. Set execution metadata `agentFinalizerMode` to `required` for a critical path that must ignore assistant text and complete only from the finalizer payload.

## Adding a contract

1. Add a focused DTO under `CanDoItAll.AgentFramework.Models`; do not create one giant nullable object.
2. Use enums for statuses and decisions.
3. Keep markdown optional and display-only.
4. Add an `IAgentOutputValidator<TOutput>` with schema and business rules.
5. Set `ExecutionRunRequest.StructuredOutput`.
6. Ensure the prompt names the DTO contract semantically and separates display markdown from machine fields.
7. Add tests for valid output, invalid output, retry/failure behavior, and markdown not driving decisions.

## Limitations

Structured output support depends on the configured provider. Providers may ignore or partially support JSON-schema response format, so post-generation validation remains mandatory. Manual and auto-approved approval continuations preserve the structured contract and are validated before a governed run can complete successfully.
