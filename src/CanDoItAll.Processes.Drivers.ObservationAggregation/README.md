# Observation Aggregation Alpha

This package aggregates already-produced verification responses. It does not invoke verifiers or inspect raw evidence payloads.

## Boundary
- Input is an in-memory `ProcessDriverObservationAggregationRequest` containing completed `ProcessDriverVerificationResponse` envelopes.
- Output is a read-only aggregate snapshot with lane summaries, diagnostic categories, evidence references, and redaction summaries.
- The aggregator rejects empty, auditless, or mixed-lane response envelopes and never mutates source responses.

## In-Memory Sample

```csharp
var request = new ProcessDriverObservationAggregationRequest(
    [transcriptResponse, runtimeResponse, officeResponse],
    DateTimeOffset.UtcNow);

var aggregate = ProcessDriverObservationAggregator.Aggregate(request);
```

The caller must pass response objects that were already produced by read-only verifiers. This package never runs drivers, discovers packages, registers services, persists observations, schedules work, or calls external systems.

## Non-Goals
- No verifier invocation, runtime host, registry, selector, provider, DI registration, manager command, scheduler hook, workflow hook, persistence, HTTP, file/directory access, workspace/storage write, finalizer/retry behavior, or process mutation.
