# Assumptions And Risks

## Critical Path Risks

1. **Wrapper-only split risk**  
   Codex could create top-level files while leaving the real logic nested in the partial class. Every split subbundle must require a source scan proving that the moved source-family logic is consumed by top-level coordinators.

2. **Hidden dispatcher dependency risk**  
   Moving nested classes out will force dependency decisions. The goal is not to pass the whole dispatch service into every coordinator. Use an internal projection host/services object with explicit operations.

3. **Behavior drift risk**  
   Projection source-family order must not change. Candidate state mutation must not change. Duplicate external-reference handling must not change. Existing focused projection tests must continue to pass.

4. **Side-effect hiding risk**  
   File reads, file copies, directory creation, storage writes, record-only artifact writes and candidate mutation must remain explicit in the coordinator that owns them. Do not hide `File.Copy`, `File.ReadAllBytesAsync`, or artifact recording inside supposedly pure rules.

5. **Premature Process Core risk**  
   This bundle still must not introduce `CanDoItAll.Processes.Core`, public contracts for projection coordinators, or `IProcessDriverPack`.

6. **Premature driver API risk**  
   It is acceptable to update a documentation-only driver-readiness map. It is not acceptable to add driver registry, driver packages, production driver interfaces, or tool-driver integration.

## Validation Risks

- A simple build is not enough. Require focused projection unit and integration tests for each moved source family.
- Source scans must verify no nested coordinator classes remain after the split phase.
- Source scans must verify no broad `ProcessRunAutomationDispatchService` instance dependency is passed into top-level coordinators except in explicitly allowed temporary compatibility subbundles.
- No browser proof should be created because this is service/runtime-only work.

## Reopen Triggers

Reopen the last moved subbundle if any of these occur:

- Projection source-family order changes.
- A projection family stops updating `ExternalReferenceKeys` or `RecordedArtifactExpectationIds`.
- A coordinator swallows exceptions that previously propagated, or throws where previous code logged and continued.
- A source-family coordinator directly constructs process-driver types or exposes a public API.
- `ArtifactProjection.cs` grows materially instead of shrinking.
- Any UI/Razor/CSS/JS file is touched.
