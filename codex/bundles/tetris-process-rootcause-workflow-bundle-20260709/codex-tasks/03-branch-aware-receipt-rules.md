# Task 03: Add branch-aware structured receipt rules

## Goal

Required tool receipts must be able to express when and why they apply.

## Model

Add a generic rule model. Suggested shape:

```csharp
public sealed record ProcessCompletionRequiredToolReceiptRule
{
    public string ToolName { get; init; } = string.Empty;
    public string RuntimeToolProviderKey { get; init; } = string.Empty;
    public int MinimumCount { get; init; } = 1;
    public bool RequireCurrentRun { get; init; } = true;
    public bool RequireSuccessfulExit { get; init; } = true;
    public string Purpose { get; init; } = "CompletionProof";
    public IReadOnlyList<string> EnforceBranchOutcomeKeys { get; init; } = [];
    public IReadOnlyList<string> SkipBranchOutcomeKeys { get; init; } = [];
    public string Reason { get; init; } = string.Empty;
}
```

Use class/record style consistent with the repository. Comments, if added, must be in English.

## Parser requirements

Support all existing formats:

- newline/semicolon delimited string,
- JSON string array,
- JSON by-step string array map.

Add support for:

- JSON object array,
- JSON by-step object array map.

Plain string rules must normalize to unconditional `CompletionProof` rules.

## Files likely affected

- `ProcessCapabilityScopeModels.cs`
- `ProcessRuntimeLaunchVariables.cs`
- `ProcessLaunchApplicationService.cs`
- `AgentFrameworkProcessLaunchExecutorResolver.cs`
- `AgentFrameworkProcessRuntimeStepAssignmentRepairService.cs`
- `AgentFrameworkProcessExecutionAdapter.ProductCompletionParsing.cs`
- `AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs`

## Acceptance

- `ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts` extracts tool names from object rules.
- `ProcessLaunchApplicationService` preserves object rules when resolving by-step maps.
- Legacy string tests still pass.
