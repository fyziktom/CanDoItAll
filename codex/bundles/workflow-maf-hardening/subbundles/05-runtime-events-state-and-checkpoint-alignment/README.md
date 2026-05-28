# 05-runtime-events-state-and-checkpoint-alignment

## Status

- `Prepared`

## Objective

Align CanDoItAll workflow runtime records with native MAF execution events, preview/durable policy, checkpoint boundaries, retry behavior, and artifacts.

## Success Criteria

- Runtime runner has distinct in-process preview and durable production paths.
- Durable-required production runs fail clearly if durable backend is not configured.
- MAF events are normalized into CanDoItAll run events with stable IDs and node/executor mapping.
- Plugin events, tool receipts, artifacts, and redaction are captured consistently.
- Retry/timeout/cancellation behavior is recorded in run events.
- Checkpoint/resume semantics are documented and tested to the extent supported by current infrastructure.

## Covered Inputs

- R05, R06, R09, R10, R11, R12, R15

## Prerequisites

- SB03 passed.
- SB04 either passed or plugin event/artifact contracts are frozen.

## Exact Source References

- Workflow runtime services found by SB01.
- `WorkflowExampleCatalogSeedService` default `WorkflowSettings` behavior.
- Persistence/run/artifact/event services found by SB01.
- MAF execution/event APIs.

## Deliverables

- Hardened `IWorkflowRuntimeRunner` or equivalent.
- Event mapper between MAF events and CanDoItAll run events.
- Artifact capture and truncation/redaction enforcement.
- Durable backend availability check.
- Tests for preview/durable policy, cancellation, retry, event order, and artifact linkage.

## Implementation Steps

1. Identify current run/event/artifact persistence model.
2. Define event taxonomy for start, node scheduled, node started, node completed, node failed, route selected, human input requested, tool approval requested, artifact captured, checkpoint created, run completed, run failed, run canceled.
3. Map native MAF `WorkflowEvent` types to repository events.
4. Preserve MAF superstep semantics in event ordering and documentation.
5. Enforce runtime policy: preview/in-process vs durable production.
6. Add tests with fake executors and fake artifact writer.
7. Update proof and execution report.

## Scope Exceptions

- Full Azure Functions hosting can remain a follow-up if not already present, but production policy must not pretend to be durable when it is not.

## Do Not Do

- Do not store large raw payloads inline beyond artifact policy limits.
- Do not log secrets or OAuth tokens.
- Do not collapse all executor events into a single text log.
- Do not ignore MAF superstep barriers when reporting concurrency/progress.

## Acceptance Checklist

- Preview run produces deterministic event/artifact records.
- Durable-required production without durable backend fails with a clear policy error.
- Cancellation and timeout are visible in run records.
- Artifact truncation/redaction policy is tested.

## Proof Required

- Runtime tests.
- Event snapshot/golden files if appropriate.
- Execution report updates.

## Progression Gate

SB06 may start after runtime contracts are stable enough for UI and migration surfaces.

## Suggested Agent Prompt

```text
Implement SB05 only. Align runtime execution, events, state, artifacts, and checkpoint policy with MAF semantics and CanDoItAll persistence.
```
