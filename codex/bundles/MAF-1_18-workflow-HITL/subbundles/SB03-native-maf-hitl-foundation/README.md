# SB03 — Native MAF Workflow HITL and Checkpoint Foundation

## Status

Prepared

## Outcome

Create a real MAF-native pause boundary for HumanInput and approval-required workflow nodes, run through streaming execution with a checkpoint manager, and prove that a disposed run can be rehydrated from a real checkpoint using an application-owned in-memory/fake checkpoint payload port.

## Owned requirements

RQ-013 through RQ-018, the foundation of RQ-019/RQ-020, RQ-022 through RQ-025.

## Non-goals

- PostgreSQL/EF production persistence;
- public API completion;
- distributed leases;
- claiming the in-process backend is durable;
- keeping exception-as-pause as the resumable implementation;
- rerunning initial workflow input on recovery;
- redesigning workflow UI;
- refactoring unrelated workflow nodes.

## Prerequisites

SB02 passed and Wave A is closed.

## Reopen triggers

- IK-07 MAF API mismatch;
- IK-08 unstable topology/identity;
- IK-09 checkpoint serialization failure;
- IK-16 zero tests;
- any design leaks MAF types into core/API contracts;
- the implementation can resume only while the original `StreamingRun` remains alive.

## Exact sources and discovery

CanDoItAll:

- `MafWorkflowCompiler.cs`
- `MafInProcessWorkflowExecutionBackend.cs`
- `WorkflowExternalRequestRuntime.cs`
- workflow runtime contracts and service registration
- `WorkflowFoundationTests`
- `MafWorkflowAdapterIsolationTests`
- `WorkflowRuntimeLifecycleRedGateTests`.

MAF 1.18 source:

- HumanInTheLoopBasic sample;
- CheckpointWithHumanInTheLoop sample;
- CheckpointAndRehydrate sample;
- `CheckpointManager`;
- `ICheckpointStore<JsonElement>`;
- `CheckpointInfo`;
- `RequestInfoExecutor`;
- `InProcessExecution`.

Verify exact API signatures after package restore.

## Implementation boundary

### 1. Introduce a framework-neutral checkpoint payload port

Define a core/abstraction contract for:

- create checkpoint payload;
- retrieve ordered checkpoint index;
- retrieve payload;
- metadata needed for workflow version/topology/request linkage.

No `Microsoft.Agents.AI.Workflows` type crosses this port.

### 2. Implement MAF JSON checkpoint adapter

In the MAF adapter project, implement MAF `ICheckpointStore<JsonElement>` over the new port and create `CheckpointManager.CreateJson(...)`.

Explicitly preserve oldest-to-newest index order.

### 3. Evolve compiler binding model

Support deterministic entry/exit/internal bindings so HumanInput and approval gates can use native request ports.

For HumanInput:

- native request payload includes the stored schema/prompt/context;
- response type is deterministic;
- port and executor IDs are stable.

For approval-required executor:

- insert a native request boundary before actual invocation;
- preserve immutable original input through checkpointed state or MAF wrapped request behavior;
- do not trust client response to resend tool/executor arguments;
- pass a scoped approval token to the existing gate/invoker to avoid duplicate prompts.

### 4. Use streaming execution

Start HITL-capable workflows through MAF streaming execution with the checkpoint manager.

Consume:

- `RequestInfoEvent`;
- `SuperStepCompletedEvent` checkpoint information;
- output, error, executor, and usage events.

Persist/capture a waiting result only when both the native request and a usable checkpoint are linked. Handle event ordering explicitly; do not assume RequestInfo always arrives before or after checkpoint without a tested correlation strategy.

### 5. Add resume-capable driver

Implement the MAF-specific external response driver/interface in a way that can:

- rebuild the exact workflow;
- resume a fresh streaming run from checkpoint;
- construct the response from persisted native request/port metadata;
- send the response;
- observe completion or next wait.

SB03 may use a deterministic fake/in-memory application store to prove protocol correctness. Production state transitions belong to SB04.

### 6. Retire the exception path for new native runs

Legacy exception/capture code may remain only for compatibility detection or non-native backends. It must not be selected for new MAF in-process HITL-capable runs.

## Acceptance criteria

- HumanInput emits a MAF `RequestInfoEvent`;
- a real MAF JSON checkpoint payload exists for the same session;
- waiting run metadata links request and checkpoint;
- a new compiled workflow instance with stable IDs resumes after the original run is disposed;
- response continues from the checkpoint rather than initial input;
- a pre-pause probe node is not rerun;
- approval-required executor is not invoked before approval;
- denial does not invoke the executor;
- original arguments cannot be replaced by response content;
- checkpoint index order is explicit and tested;
- topology fingerprint is deterministic;
- incompatible topology fails before MAF continuation;
- backend remains `IsDurable = false`;
- `SupportsExternalResponseResume` becomes true only when the real driver is registered and tests pass;
- generic runtime contracts remain framework-neutral;
- no large god-class expansion substitutes for focused adapters.

## Proof tier

Governed

## Focused validation

New/extended filters:

- `MafJsonCheckpointStoreAdapterTests`
- `MafWorkflowHumanInLoopTests`
- `MafWorkflowAdapterIsolationTests`
- `WorkflowFoundationTests`
- affected lifecycle tests.

Required realistic proof:

1. start workflow;
2. execute marker before HumanInput;
3. capture request and checkpoint;
4. dispose run/backend instance;
5. create new backend/compiler instance;
6. resume from checkpoint;
7. submit response;
8. complete;
9. assert pre-HITL marker executed once.

Negative proof:

- wrong checkpoint session;
- wrong request/port;
- topology mismatch;
- missing payload;
- response argument tampering;
- denial.

Create `proof/SB03/manifest.md` and sanitized command/result/source assertion artifacts.

## Invalidation keys

IK-07, IK-08, IK-09, IK-16, IK-17.

## Broad-gate decision

No broad gate. Build workflow abstractions/runtime/MAF adapter and the unit project. Full persistence/API integration is not ready.

## Closure record

Not executed.

Record:

- MAF APIs used:
- new abstraction/adapters:
- compiler binding identity scheme:
- request/checkpoint correlation:
- rehydration proof:
- approval/denial proof:
- negative cases:
- source assertions:
- tests/counts:
- blockers/deviations:
