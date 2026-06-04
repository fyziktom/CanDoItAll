# 03-streaming-events-and-node-identity

## Status

- Status: `Completed`

## Objective

Improve MAF event consumption and persistence so workflow runs can be debugged, observed, resumed, and audited accurately.

## Covered Inputs

- R4: Consume streaming MAF events where needed and persist typed event metadata with executor/node identity.
- R2: Preserve request state needed by execution-position HITL.
- R5: Provide event foundation for checkpoint capture.
- R6: Coordinate event payload bounds with artifact policy.

## Prerequisites

- SB02 HITL and approval request behavior is completed or honestly blocked.
- Event model source references still match current repo state.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/CanDoItAll.AgentFramework.Persistence.csproj`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`

## Scope

- Introduce MAF event normalization with typed pattern matching where APIs permit it.
- Preserve executor id, node id, request id, output payload/reference, and redacted error summaries.
- Use streaming execution when needed for request handling, progress, checkpoints, or UI observability.

## Dependency Impact

- SB04 checkpoint metadata, SB05 artifact policy, and SB07 backend honesty depend on reliable runtime event records.

## Validation Depth

- Unit normalizer tests plus runtime/integration proof on multi-node workflows.
- Critical proof requires production behavior artifact matrix for new event records/states.

## Implementation Steps

1. Introduce `IMafWorkflowEventNormalizer`.
2. Normalize workflow lifecycle, executor, superstep, request, output, warning, and error events.
3. Preserve executor id and map it to workflow node id using compiler binding metadata.
4. Stop using `ToString()` as the primary persisted event message.
5. Use streaming execution where request/progress/checkpoint observability needs it.
6. Deduplicate or clearly label CanDoItAll progress events versus native MAF events.
7. Coordinate redaction and payload bounds with SB05.

## Do Not Do

- Do not persist raw exceptions or secrets.
- Do not store unbounded event payloads inline.
- Do not break existing event API consumers.

## Acceptance Checklist

- Executor events include node and executor identity.
- Output events include final output or artifact reference.
- Error events include redacted exception summary.
- Request events include request id/kind and can be surfaced by UI/API.

## Proof Required

- Unit tests for event normalizer.
- Runtime test showing node/executor identity on a multi-node workflow.
- Integration or component test showing improved timeline fields.
- `bundle://proof/SB03/manifest.md` and `bundle://proof/SB03/semantic-invariants.md`.

## Browser Validation Logging

- Browser proof is required only if the timeline UI is changed; otherwise component/API assertions are sufficient.

## Progression Gate

- Continue to SB04 only after event records can reliably support checkpoints, request state, artifact references, and debugging identity.
- Result: `Passed`. Proof is captured in `bundle://proof/SB03/manifest.md` and `bundle://proof/SB03/semantic-invariants.md`.
- Runtime note: the current in-process implementation consumes MAF `Run.OutgoingEvents` plus CanDoItAll progress/request capture. A separate streaming session is not required for the SB03 proof path and remains available to SB04 if checkpoint/resume capture needs it.

## Suggested Agent Prompt

Add typed MAF event normalization and prove persisted workflow event records carry useful node, executor, request, output, and redacted error metadata.
