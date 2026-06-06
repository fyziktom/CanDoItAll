# Acceptance criteria

A completed implementation must prove:

1. The solution builds.
2. Focused projection unit tests pass.
3. Focused artifact projection integration tests pass.
4. Source scans show no Process Core and no production driver APIs.
5. Source scans show no UI or prohibited viewport proof drift.
6. Source scans show no TODO, stub, NotImplemented, default placeholder, or fixture-specific production shortcuts.
7. `IProcessArtifactProjectionHost` is either removed or reduced to a small temporary bridge with a documented removal condition.
8. No source-family coordinator depends directly on `ProcessRunAutomationDispatchService` or on the broad host interface.
9. Projection source-family order is proven by a source assertion and a focused test.
10. Candidate mutation is still centralized and consistent.
11. Completed-stage validator passes.
