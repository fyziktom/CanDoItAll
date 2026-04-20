# Universal WebGL library skeleton and typed contracts

## Status

- Completed

## Objective

- Create the new universal WebGL RCL, wire it into the solution, and define generic scene and event contracts without any Processes dependency.

## Covered Inputs

- `IN-05`
- `IN-06`
- `IN-11`
- `RQ-04`
- `RQ-05`
- `RQ-06`
- `RQ-07`
- `RQ-10`
- `RQ-22`

## Prerequisites

- `01-baseline-and-renderer-decision-lock`

## Exact Source References

- C:/repositories/CanDoItAll/CanDoItAll.slnx
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchSurface.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchEvents.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchUiState.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Components/Shared/Assets/CanvasLibHeadAssets.razor
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Components/Shared/Assets/CanvasLibBodyAssets.razor
- C:/repositories/CanDoItAll/tools/canvaslib/asset-manifest.json

## Deliverables

- New `CanDoItAll.Components.WebGlLib` project in the solution with no Processes reference.
- Typed generic contracts for scene, nodes, edges, camera/UI state, diagnostics, and semantic interaction events.
- Asset-loading pattern and project skeleton consistent with repository conventions.

## Dependency Impact

- All runtime, sandbox, and automation work depends on these contracts remaining generic and stable.
- If process-specific models leak into the library now, the architecture gate must fail.

## Validation Depth

- High
- Build + architecture guard + contract-focused tests

## Implementation Steps

1. Add the new RCL project and wire it into `CanDoItAll.slnx`.
2. Mirror the existing canvas library's typed-surface pattern while renaming the concepts to generic WebGL scene contracts.
3. Add asset-loader components or equivalent runtime bootstrapping consistent with the existing repository pattern.
4. Add small tests or guards proving the library compiles without depending on `CanDoItAll.Modules.Processes`.


## Do Not Do

- Do not implement process-template projection inside the library.
- Do not add a direct reference from the library to the Processes module.
- Do not start with a giant scene contract that already hardcodes process node kinds.

## Acceptance Checklist

- The new library is solution-integrated and compiles independently.
- The contracts are generic enough to host non-process scenes later.
- The asset strategy is deterministic and documented.

## Proof Required

- Build the solution or the new library project.
- Add or update tests proving no forbidden Processes reference exists and the contracts serialize/round-trip cleanly.
- Capture architecture notes showing the library boundary stayed generic.
- Validation commands to run for this subbundle:
- `dotnet build CanDoItAll.slnx -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~WebGl|FullyQualifiedName~ProjectStructure" -v:minimal`

## Browser Validation Logging

- A browser route is not required yet, but a tiny smoke host is allowed if needed to confirm asset loading.
- Any temporary smoke host must not be mistaken for the final sandbox.

## Progression Gate

- Downstream work may continue only after the library compiles, the contracts are typed and generic, and no Processes dependency leaks into the new RCL.

## Suggested Agent Prompt

```text
Implement only subbundle 02. Add the universal WebGL RCL, wire it into the solution, define typed generic contracts and asset-loading scaffolding, prove the library has no Processes dependency, and stop before process-template projection or sandbox work.
```

## Preserved Bundle Notes

### Review questions

- Is the new library truly universal?
- Did the typed contracts stay generic and small enough to evolve safely?
- Is the asset strategy repository-native and deterministic?

### Validation commands

- `dotnet build CanDoItAll.slnx -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~WebGl|FullyQualifiedName~ProjectStructure" -v:minimal`

### Corrective trigger

- If this subbundle fails, open `_corrective-renderer-boundary-reset` before continuing downstream.

### Corrective template

- `subbundles/_corrective-renderer-boundary-reset`

### Repository touchpoints (relative)

- `CanDoItAll.slnx`
- `src/CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj`
- `src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor`
- `src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchSurface.cs`
- `src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchEvents.cs`
- `src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchUiState.cs`
- `src/CanDoItAll.Components.CanvasLib/Components/Shared/Assets/CanvasLibHeadAssets.razor`
- `src/CanDoItAll.Components.CanvasLib/Components/Shared/Assets/CanvasLibBodyAssets.razor`
- `tools/canvaslib/asset-manifest.json`

### Notes

- Treat this subbundle as an isolated execution slice. Do not continue into later numbered work during the same pass.
- Update `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md` as soon as this subbundle either passes, blocks, or triggers a corrective path.
