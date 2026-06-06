# Seam map

| Seam | Current transition state | Target after this bundle | Future Core relevance |
| --- | --- | --- | --- |
| Route handlers | Top-level handlers with route facets | Handlers depend on focused route services and route models only | Route decision rules may later move to Core |
| Route services | Split classes but forwarding to dispatcher | Real service ownership for database requirement, upstream materialization, subprocess runtime, start transition, run-closed guard, and finalizer handoff | Application layer boundary |
| Route factory | Broad facet-set input | Explicit facet parameters per handler dependency | Handler composition remains application-local |
| Candidate hydration | Dispatcher method | `ProcessDispatchCandidateHydrationService` with snapshots and explicit dependencies | Hydration stays application/infrastructure, pure selection rules can move later |
| Direct-agent binding | Inline in candidate hydration | Binding coordinator/service boundary | Application layer, not Core |
| Subprocess runtime | Dispatcher partial + helper coordinators | `ProcessDispatchSubprocessRuntimeService` and projection writer/gap coordinators | Subprocess status mapping may move to Core later |
| Pre-execution guard | helper boundary plus dispatcher wrappers | Route services own DB requirement/materialization route effects through `ProcessDispatchPreExecutionGuardHandler` | Application layer |
| Transition/finalizer | dispatcher partial methods | `ProcessDispatchFinalizerApplicationService` plus top-level finalizer context factory | Some transition rules may later move to Core |
| Failure closure | exception closure partial | FailureClosureCoordinator with explicit inputs | Application layer |
| Static helpers | many dispatcher static wrappers | rule helpers own logic; dispatcher wrappers thin or removed | Pure rules later Core candidates |
