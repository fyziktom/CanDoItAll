# SB012 Critical Gate Manifest

- Gate: pre-execution route handler host cleanup.
- Result: closed.
- Database requirement and upstream materialization effects moved into route services using `ProcessDispatchPreExecutionGuardHandler` and `ProcessDispatchStepTransitionService`.
- Start transition reload path uses `ProcessDispatchCandidateHydrationService`.
