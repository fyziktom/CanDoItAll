# Assumptions And Risks

## Assumptions

- The branch is `maf-processes-refactor`.
- Previous helper boundaries must remain intact.
- `ArtifactProjection.cs` is still a private dispatcher partial and can be decomposed inside `CanDoItAll.Modules.Processes`.
- Existing tests around artifact projection, artifact validation, recovery routing, subprocess projection, and finalizer behavior are available.
- Some broad architecture tests may still have old historical bundle fixture coupling; this should be documented if still present, not silently ignored.

## Critical Path Risks

1. **Projection order drift**
   - Source family order must not change.
   - Candidate external reference / expectation state must be updated in the same observable way.

2. **Side-effect hiding**
   - File reads, storage writes, DB writes, and `RecordArtifactAsync` must only move into coordinator classes whose names clearly indicate side effects.

3. **Over-eager Core/Driver extraction**
   - Do not move private dispatcher DTOs into public contracts.
   - Do not create `IProcessDriverPack` or driver registry.

4. **Test dilution**
   - A compile-only pass is insufficient.
   - Every source family migration requires focused positive and negative tests.

5. **Line-count-only refactor**
   - Reducing lines without extracting stable source-family boundaries is not enough.

## Validation Risks

- Existing broad test class failures due to historical bundle fixtures must be separated from this work.
- Focused tests must cover behavior, not just helper existence.
- Source scans must verify there are no hidden UI/proof artifacts.

## Reopen Triggers

Reopen the last production subbundle if:

- projection order changes;
- `new ProcessArtifactRecord` appears in a planner instead of a coordinator;
- `File.ReadAllBytes*`, `File.Exists`, or storage write logic is added to a pure source adapter/planner;
- `IProcessDriverPack` or `CanDoItAll.Processes.Core` appears;
- UI files or mobile proof artifacts are created;
- full build fails;
- focused projection tests fail;
- `ProjectExecutionArtifactsAsync` still owns source-family internals after SB52.
