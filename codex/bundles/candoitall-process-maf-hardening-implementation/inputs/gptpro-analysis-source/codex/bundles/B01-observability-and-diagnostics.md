# B01 — Observability and diagnostics hardening

## Goal

Make blocked operator actions and rework prompts use exact step-level diagnostics. Never lose the real cause just because AgentFramework observation lookup missed the step or `ResultSummary` is empty.

## Current problem

`ProcessExecutionObservationQuery` only supports run-level filtering. `AgentFrameworkProcessExecutionObservationReader` takes N latest execution runs per process run. In a large nested process, the blocked step can be older than the latest N records and disappear from the observation set.

The UI then shows:

```text
No AgentFramework result summary was found for this blocker
```

instead of the concrete issue.

## Required changes

### 1. Extend observation query

Add step-level filtering without breaking existing call sites.

Suggested shape:

```csharp
public sealed record ProcessExecutionObservationQuery(
    IReadOnlyList<ProcessRunId> RunIds,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int TakePerRun,
    IReadOnlyList<ProcessStepInstanceId>? StepInstanceIds = null);
```

If a richer selector is cleaner, use a small selector record containing run id and optional step id.

### 2. Query AgentFramework by exact step id

Update `AgentFrameworkProcessExecutionObservationReader.ListExecutionRunsAsync(...)` so operator-action calls can query:

```csharp
new ExecutionRunQuery(
    Take: Math.Max(1, query.TakePerRun),
    ProcessRunId: runId.ToString(),
    ProcessStepId: stepId.ToString(),
    UpdatedFromUtc: query.FromUtc,
    UpdatedToUtc: query.ToUtc)
```

If the underlying query API does not currently support `ProcessStepId` in this overload, add it there too.

### 3. Use exact-step observations for operator action diagnostics

In `ProcessRuntimeProjectionQueryService`, when building operator actions for blocked/failed steps:

- collect exact step ids,
- query observations with those step ids,
- fallback to run-level observations only for general live dashboard enrichment.

### 4. Add runtime receipt fallback

When no AgentFramework observation is found, build diagnostics from the last `StrategyResultReceipt`:

- outcome,
- applied step status,
- diagnostic code(s),
- diagnostic summary,
- requested artifact slot(s),
- manager signal(s),
- recovery decision,
- result hash.

Create a focused helper, for example:

```csharp
internal interface IProcessBlockedStepPacketBuilder
{
    ProcessBlockedStepPacket Build(...);
}
```

Do not keep adding logic into `ProcessRuntimeProjectionQueryService`.

### 5. Fix generic capability hint

`BuildOperatorCapabilityHint` currently uses `assignment.RequiredArtifactSlotIds.Count`, which is misleading when the missing thing is an expected produced output. It should use a blocked packet that distinguishes:

- missing input artifact,
- missing expected output artifact,
- required runtime tool missing,
- child subprocess no accepted handoff,
- child run still active,
- no diagnostics available.

## Tests

Add tests equivalent to:

- `ObservationReader_WhenManyExecutionRunsExist_FindsExactBlockedStep`
- `OperatorAction_WhenAgentFrameworkObservationMissing_UsesRuntimeReceiptDiagnostics`
- `OperatorAction_WhenMissingExpectedOutput_ShowsProducedArtifactExpectation`
- `OperatorAction_DoesNotRecommendBlindRetry_WhenDiagnosticMissing`

## Acceptance criteria

- The operator problem summary no longer says only “No AgentFramework result summary…” when runtime has diagnostic receipts.
- Exact AgentFramework execution run id is shown when available.
- Rework prompt contains a blocked-step packet with step key, step id, process run id, last strategy outcome, diagnostic code and exact next action.
