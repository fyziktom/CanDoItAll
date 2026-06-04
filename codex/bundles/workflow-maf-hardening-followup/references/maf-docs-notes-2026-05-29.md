# MAF docs notes checked 2026-05-29

These notes are for Codex orientation. Re-check official docs if implementation depends on exact API signatures.

## Workflows overview

MAF workflows are explicitly-defined business process graphs with type safety, graph-based executors/edges, external integration, checkpointing, multi-agent orchestration, events, HITL, and checkpoint support.

## Workflow Builder & Execution

- `WorkflowBuilder` builds directed workflow graphs.
- `InProcessExecution.RunStreamingAsync` exposes events as they happen.
- `InProcessExecution.RunAsync` waits for completion.
- MAF validation covers type compatibility, graph connectivity, executor binding, and edge validation.
- Superstep execution provides deterministic barriers and checkpoint boundaries.

## Executors

- C# recommended executor style uses `partial` classes deriving from `Executor` with `[MessageHandler]`.
- Function-based executors are supported, but source-generated executors are preferred for compile-time validation, performance, and Native AOT compatibility.
- CanDoItAll dynamic graph nodes can still justify function-bound adapter executors, but the choice should be explicit and documented.

## Human-in-the-loop

- HITL is represented through request/response handling.
- `RequestPort` emits `RequestInfoEvent`.
- External systems listen for requests and send responses back to the run.
- Pending requests are part of checkpoint state and re-emitted after restore.

## Events

- MAF emits workflow lifecycle events, executor invoked/completed/failed events, superstep events, request info events, and custom events.
- Streaming event consumption exposes executor id and data in concrete event types.
- CanDoItAll should not collapse these into `ToString()` strings.

## Checkpoints

- Checkpoints capture executor state, pending messages, pending requests/responses, and shared states.
- A checkpoint manager/storage is required.
- Checkpoint storage is a trust boundary and must be treated as private trusted infrastructure.
