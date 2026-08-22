# SB03 — Native MAF Workflow HITL and Checkpoint Foundation

## Status

Proven

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
- public `RequestPort.Create<TRequest,TResponse>`, `RequestPortBinding`, and `BindAsExecutor` APIs (`RequestInfoExecutor` is internal in MAF 1.18 and is not an implementation option);
- `InProcessExecution`.

Verify exact API signatures after package restore.

## C# Architecture Impact

This is a critical foundation extraction. It replaces exception-as-pause for new native runs with deterministic MAF request-port topology, real JSON checkpoints, streaming event correlation, and fresh-run response rehydration. It must reduce responsibility in `MafWorkflowCompiler` and `MafInProcessWorkflowExecutionBackend`; adding branches or partial files to those types is not acceptable.

The durable design records are `../../architecture/00-csharp-current-state-inventory.md` through `../../architecture/04-csharp-testability-plan.md`. CP-WB0 in `../../plan/architecture-checkpoints.md` is Prepared / entry-approved; CP-WB1 is Proven / Pass with governed evidence under `../../proof/SB03`.

## Boundary Ownership

- Models/Workflows.Abstractions own SDK-neutral checkpoint, request-link, version/topology, response, and approval-authorization records and ports.
- MafAdapter alone owns MAF request ports, checkpoint manager/store adapter, stream events, checkpoint translation, topology builder, correlator, and rehydration driver.
- `MafWorkflowCompiler` remains a thin compile facade and delegates deterministic HITL binding construction.
- `MafInProcessWorkflowExecutionBackend` remains the interface-facing execution/resume backend because runtime selects and casts the same instance; resume is a thin delegation to a concrete driver.
- Runtime supplies only the explicit in-memory checkpoint-payload implementation required for SB03 proof. PostgreSQL remains SB04.

New request records use distinct typed version/state, response contract, backend request/checkpoint link, and safe authorization-policy snapshot fields. `RequestJson` is not reused as a catch-all. Legacy rows without native linkage remain explicitly non-resumable.

## Dependency Direction

No new project reference is planned. Neutral projects must not reference MAF. Runtime must not reference MafAdapter. MafAdapter depends inward on neutral workflow/executor contracts and outward on MAF. A post-change CodeAnalytics snapshot must show no new cycle/reference edge relative to focused baseline `snap-20260820220112-5cb38069`, an acyclic scoped project graph, and the two named baseline non-project cycles unchanged.

## Pattern Decision

- Adapter: MAF `ICheckpointStore<JsonElement>` over the neutral payload port.
- Builder: immutable deterministic entry/exit/internal HITL bindings behind the existing compiler facade.
- State: explicit request/checkpoint correlation independent of event arrival order.
- Concrete driver plus verifier: fresh-run native response rehydration behind the existing backend facade.

Rejected approaches include partial-class growth, another compiler/manager, process-local checkpoint durability, event-order assumptions, retained original streaming runs, and any restart-from-initial-input fallback. Full records are in `../../architecture/03-csharp-pattern-selection-records.md`.

## Testability Contract

Direct tests instantiate the checkpoint adapter, HITL binding compiler, correlator, rehydration verifier/driver, and approval authorization logic without `WorkflowRuntimeManager`, a host, or a database. Integration proof must use the real MAF request/checkpoint/response protocol, dispose all first-run objects, reconstruct fresh instances, and prove the pre-wait marker is not rerun. A fake resume backend or metadata checkpoint is not proof.

## Partial Class Policy

No handwritten production partial or nested extracted service is allowed. Generated files are not expected in SB03. The old compiler/backend must materially delegate and shrink or remain a demonstrably thin facade; moving behavior into a partial/helper while retaining decisions fails the gate.

## Architecture Proof Required

- before/after project dependency and CodeAnalytics cycle evidence;
- source isolation for MAF types and no new production partials;
- direct unit and negative tests for every extracted responsibility;
- source assertion that new native paths neither throw nor catch `WorkflowExternalRequestPendingException`;
- compiler/backend responsibility and line-count comparison;
- executable production-composition smoke;
- real MAF request plus real JSON checkpoint plus disposed-run resume proof;
- descriptor advertises resume only with the real driver and keeps `IsDurable = false`;
- CP-WB1 reviewer decision and downstream SB04 unlock only after governed proof is complete.

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
- preserve immutable original input in the checkpointed native request and create a server-owned continuation response from the restored request plus the validated decision; exact 1.18 inspection proves wrapped request mode does not preserve the original payload;
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

Proven on 2026-08-20. Governed evidence is under `../../proof/SB03`; CP-WB1 is Pass.

- MAF APIs used: public `RequestPort.Create<TRequest,TResponse>`,
  `BindAsExecutor(allowWrappedRequests: false)`, `CheckpointManager.CreateJson`,
  `ICheckpointStore<JsonElement>`, streaming watch, and fresh-run streaming resume.
- New abstractions/adapters: framework-neutral typed checkpoint payload/index records and
  store, MAF JSON checkpoint adapter, deterministic HITL binding compiler, request/checkpoint
  correlator, native start driver, response driver, rehydration verifier, and turn-result mapper.
- Compiler binding identity scheme: stable definition/version/node/port identities plus a
  deterministic topology fingerprint are rebuilt from the exact catalog version.
- Request/checkpoint correlation: a waiting result is emitted only after the same session's
  native request and usable checkpoint are linked; both event arrival orders are tested.
- Rehydration proof: the original run is disposed, fresh compiler/backend instances resume
  from the real JSON checkpoint, and the pre-wait marker remains at one execution.
- Approval/denial proof: approval invokes the governed executor once; typed denial completes
  without invocation; original arguments and the scoped token remain server-owned.
- Negative cases: explicit mutation tests cover wrong session, request, port, topology,
  missing payload, corrupt payload, and response tampering. The verifier also validates the
  remaining exact identity fields; this closure does not claim a dedicated mutation negative
  for every validated field.
- Tests/counts: the exact Debug selector passed 203/203 across
  `WorkflowBackendCheckpointPayloadStoreTests`, `MafJsonCheckpointStoreAdapterTests`,
  `MafHumanInputCheckpointCorrelatorTests`, `MafWorkflowHitlBindingCompilerTests`,
  `MafWorkflowHumanInLoopTests`, `MafWorkflowTurnResultMapperTests`,
  `MafNativeHitlArchitectureTests`, `MafWorkflowAdapterIsolationTests`,
  `WorkflowFoundationTests`, `WorkflowRuntimeLifecycleRedGateTests`,
  `WorkflowExecutorTests`, `MafWorkflowExecutorFailureDiagnosticsTests`, and
  `MafWorkflowEventNormalizerTests`.
- Builds and static gates: the Debug unit build passed; Models, executor abstractions/core,
  workflow abstractions/runtime, and MafAdapter passed six sequential Release builds; the
  Release unit build also passed, for seven Release builds total. The upgraded package
  scanner, documentation validation, bundle validator, source/anti-stub assertions, and
  `git diff --check` passed.
- Architecture: final snapshot `snap-20260821002934-bf844210` has no project cycle and
  exactly the two unchanged baseline non-project cycles. Strict CP-WB1 review passed after
  native start and turn-result mapping were extracted into directly tested top-level types.
- Blockers/deviations: no SB03 blocker remains. The first sandboxed Release unit build could
  not write sibling Components generated-asset caches; the authorized identical rerun passed
  with zero warnings and errors. Persistent restart durability, response-operation CAS/lease,
  cancellation/failure races, legacy-row recovery, and public API authorization remain owned
  by SB04/SB05 and are not claimed here.
