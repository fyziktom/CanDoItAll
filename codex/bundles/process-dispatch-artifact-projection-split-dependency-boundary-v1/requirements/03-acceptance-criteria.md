# Acceptance Criteria

1. `ProjectExecutionArtifactsAsync` remains behaviorally equivalent and calls projection families in the same order.
2. `ProcessRunAutomationDispatchService.ArtifactProjectionCoordinators.cs` no longer contains all coordinator implementations as nested private classes; either it is deleted or reduced to a very small compatibility shim.
3. Each source-family coordinator is an internal top-level module-local class in `CanDoItAll.Modules.Processes`.
4. Top-level coordinators do not accept the full `ProcessRunAutomationDispatchService` unless a subbundle explicitly marks it as temporary and a later subbundle removes it.
5. Side effects are visible in coordinator names and source scans:
   - file read/copy/write,
   - storage write,
   - record-only artifact write,
   - candidate-state mutation.
6. Focused unit and integration projection tests pass.
7. Full solution build passes.
8. Source scans prove no Process Core, no production driver API, no UI diff, no prohibited viewport proof, no stubs.
9. Documentation-only driver-readiness map is updated.
10. Execution report records every subbundle gate, proof, and raw note closure.
