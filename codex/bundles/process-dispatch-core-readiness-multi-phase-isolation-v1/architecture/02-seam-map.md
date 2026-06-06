# Seam map

| Seam | Current transition state | Target after this bundle | Future Core relevance |
| --- | --- | --- | --- |
| Route handlers | Top-level handlers with route facets | Handlers depend on focused route services and route models only | Route decision rules may later move to Core |
| Route services | Split classes but forwarding to dispatcher | Real service ownership for selected side effects, adapter only at edge | Application layer boundary |
| Candidate hydration | Dispatcher method | CandidateHydrationService with snapshots and explicit dependencies | Hydration stays application/infrastructure, pure selection rules can move later |
| Direct-agent binding | Inline in candidate hydration | Binding coordinator/service boundary | Application layer, not Core |
| Subprocess runtime | Dispatcher partial + helper coordinators | SubprocessRuntimeService and projection store/writer | Subprocess status mapping may move to Core later |
| Pre-execution guard | helper boundary plus dispatcher wrappers | PreExecutionGuardService owns DB requirement/materialization route effects | Application layer |
| Transition/finalizer | dispatcher partial methods | TransitionFinalizerApplicationService | Some transition rules may later move to Core |
| Failure closure | exception closure partial | FailureClosureCoordinator with explicit inputs | Application layer |
| Static helpers | many dispatcher static wrappers | rule helpers own logic; dispatcher wrappers thin or removed | Pure rules later Core candidates |
