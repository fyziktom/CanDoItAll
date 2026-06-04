# Process Execution Snapshot Shape

The implementation agent should design the minimum neutral records needed by current dispatcher code. Do not invent a rich domain model.

Likely records:

```csharp
public sealed record ProcessAutomationExecutionResult(
    Guid ExecutionRunId,
    Guid? ChatSessionId,
    string ResponseText);

public sealed record ProcessAutomationExecutionDetail(
    ProcessAutomationExecutionRunSnapshot Run,
    IReadOnlyList<ProcessAutomationToolReceiptSnapshot> ToolReceipts,
    IReadOnlyList<ProcessAutomationArtifactSnapshot> Artifacts,
    string ResponseText,
    ... only fields currently consumed ...);

public sealed record ProcessAutomationExecutionRunSnapshot(
    Guid Id,
    Guid ChatSessionId,
    string State,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    ...);

public sealed record ProcessAutomationToolReceiptSnapshot(
    string ToolName,
    bool Succeeded,
    string RequestJson,
    string ResponseJson,
    string RuntimeToolProviderKey,
    string RuntimeToolProviderName,
    DateTimeOffset CreatedAtUtc,
    ...);
```

Exact names may differ, but contracts must remain neutral and source-backed by current consumers.
