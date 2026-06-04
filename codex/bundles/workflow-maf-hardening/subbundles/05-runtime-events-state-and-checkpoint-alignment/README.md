# 05-runtime-events-state-and-checkpoint-alignment

## Status

- `Completed`

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

- `repo://src/CanDoItAll.AgentFramework.Maf`
- `repo://src/CanDoItAll.Modules.AgentFramework`
- `repo://src/CanDoItAll.AgentFramework.Core`
- `repo://src/CanDoItAll.AgentFramework.Models`
- `repo://src/CanDoItAll.AgentFramework.Persistence`
- `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowExampleCatalogSeedService.cs`
- `repo://tests/CanDoItAll.Tests.Unit`
- `bundle://references/maf-2026-05-28-source-notes.md`

## Deliverables

- Hardened `IWorkflowRuntimeRunner` or equivalent.
- Event mapper between MAF events and CanDoItAll run events.
- Artifact capture and truncation/redaction enforcement.
- Durable backend availability check.
- Tests for preview/durable policy, cancellation, retry, event order, and artifact linkage.

## Dependency Impact

- SB06 UI and migration work depends on the runtime policy, event taxonomy, artifact linkage, and durable-backend status defined here.
- SB07 final observability review depends on this phase exposing stable source assertions and testable runtime records.
- If event or policy semantics change later, SB05 must be reopened before final closure.

## Validation Depth

- Critical foundation with semantic proof required under `proof/SB05/manifest.md` and `proof/SB05/semantic-invariants.md`.
- Requires negative durable-production-without-backend proof, positive preview event/artifact proof, cancellation/timeout proof, source assertions, and anti-stub audit.
- Requires a production behavior artifact matrix for any new runtime event, state, record, or lifecycle signal.

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

## Browser Validation Logging

- N/A unless runtime policy or event visibility is exposed in UI during this subbundle; otherwise SB06 owns browser validation.

## Progression Gate

- SB06 may start after runtime contracts are stable enough for UI and migration surfaces and SB05 closure proof cites `proof/SB05/manifest.md` plus `proof/SB05/semantic-invariants.md`.

## Suggested Agent Prompt

```text
Implement SB05 only. Align runtime execution, events, state, artifacts, and checkpoint policy with MAF semantics and CanDoItAll persistence.
```
