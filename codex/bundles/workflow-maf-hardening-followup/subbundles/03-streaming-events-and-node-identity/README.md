# 03-streaming-events-and-node-identity

## Objective

Improve MAF event consumption and persistence so workflow runs can be debugged, observed, resumed, and audited accurately.

## Current problem

The in-process backend uses non-streaming `RunAsync` and post-processes `OutgoingEvents`. Native MAF events are persisted with `NodeId: null`, generic `ToString()` messages, and reflection-based payload extraction. This loses executor/node identity and request/output/error semantics.

## Exact source references

- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`
- `src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowRuntimeModels.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `src/CanDoItAll.AgentFramework.Persistence/*`
- `tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs`
- `tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`

## Implementation steps

1. Introduce `IMafWorkflowEventNormalizer`.
2. Normalize known MAF event types with typed pattern matching:
   - workflow started/output/error/warning,
   - executor invoked/completed/failed,
   - superstep started/completed,
   - request info,
   - agent response/update if available.
3. Preserve `ExecutorId` and map it back to `WorkflowNodeId` via compiler binding metadata.
4. Stop relying on event `ToString()` as the primary persisted message.
5. Use streaming execution where needed for request handling, progress, checkpoints, or UI observability.
6. Deduplicate CanDoItAll progress events and native executor events or clearly label them as separate event streams.
7. Add redaction and inline payload bounds to event normalization, coordinated with SB05.

## Do not do

- Do not persist raw exceptions with secrets.
- Do not store unbounded event payloads inline.
- Do not break existing event API consumers; add fields in compatible ways or provide migration logic.

## Acceptance checklist

- Event records for executor events include node/executor identity.
- Output events include final output or artifact reference.
- Error events include redacted exception summary.
- Request events include a request id/kind and can be surfaced by UI/API.
- Component/integration tests assert user-visible event timeline quality.

## Proof required

- Unit tests for event normalizer.
- Runtime test showing node/executor identity on a multi-node workflow.
- Integration/UI test showing improved timeline fields.
