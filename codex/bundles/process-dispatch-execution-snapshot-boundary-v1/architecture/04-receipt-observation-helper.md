# Receipt Observation Helper

Add a small process-module-local helper that consumes process-owned execution snapshots and returns normalized observations used by dispatcher consumers.

Possible helper:

```csharp
internal sealed class ProcessExecutionReceiptObservationService
{
    IReadOnlySet<string> ResolveSuccessfulToolNames(ProcessAutomationExecutionDetail detail);
    IReadOnlyList<ProcessAutomationToolReceiptSnapshot> FindReceipts(...);
    bool HasProviderToolFamily(...);
}
```

Do not move artifact projection or validation wholesale. Only centralize logic that currently repeatedly inspects execution details/receipts for successful tool names, provider metadata, and required-tool family checks.
