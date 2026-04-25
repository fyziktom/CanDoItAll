# Process template projection and center-lane 3D scene adapter

## Status

- Completed

## Objective

- Build the process-specific adapter that projects real template processes into the generic WebGL scene while reusing the current process IDs, categories, and branching semantics.

## Covered Inputs

- `IN-08`
- `IN-17`
- `IN-18`
- `RQ-12`
- `RQ-13`
- `RQ-19`

## Prerequisites

- `04-architecture-review-gate-a`

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessDefinitionEditorModels.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessCanvasCatalog.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessCanvasBranching.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessDependencyCompatibilityBridge.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessTemplatePackLoader.cs
- C:/repositories/CanDoItAll/Templates/Processes/manifest.json

## Deliverables

- Process-template-to-WebGL scene adapter outside the universal library.
- Deterministic center-lane 3D layout rules that reuse current process positions, spread roles around the lane, and add semantic depth.
- Visual-style mapping for process connection categories and branching semantics.

## Dependency Impact

- The dedicated sandbox depends on this adapter to load real template content.
- If mapping semantics drift from the current process IDs or categories, interaction proof becomes misleading.

## Validation Depth

- High
- Focused adapter tests + scene snapshot review

## Implementation Steps

1. Use the existing template pack services to materialize `ProcessDefinitionEditorModel` instances for selected templates.
2. Project roles, steps, branch routers, and connections into the generic scene contract while preserving stable IDs.
3. Define deterministic depth offsets and grouping rules so the scene remains readable and testable.
4. Add tests that compare projected node/edge counts and category mappings to the source template semantics.


## Do Not Do

- Do not move process-specific projection code into the universal library.
- Do not invent new IDs when the current process helpers already provide stable identifiers.
- Do not add persistence or workspace mutations.

## Acceptance Checklist

- At least three representative templates can be projected into the generic scene contract.
- Process IDs and connection categories are preserved or explicitly mapped.
- The 3D lane rules are deterministic enough for testing and screenshots.

## Proof Required

- Run focused tests for template projection and scene adaptation.
- Capture scene snapshots for simple, medium, and dense templates and review node/edge counts.
- Record any semantic gaps found during projection.
- Validation commands to run for this subbundle:
- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj --filter "FullyQualifiedName~ProcessTemplate|FullyQualifiedName~WebGl" -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessCanvasSurfaceFactory|FullyQualifiedName~WebGl" -v:minimal`

## Browser Validation Logging

- Optional at this stage if the sandbox host is not ready yet, but scene snapshots for the chosen templates are required.

## Progression Gate

- Sandbox implementation may continue only after the adapter proves it can load representative templates with stable IDs and deterministic depth rules.

## Suggested Agent Prompt

```text
Implement only subbundle 05. Build the process-template adapter outside the universal library, reuse current process IDs and categories, define deterministic center-lane 3D depth rules, test the projection on representative templates, and stop before adding the dedicated sandbox UI.
```

## Preserved Bundle Notes

### Review questions

- Did the adapter preserve current IDs and semantics?
- Are the depth rules deterministic and readable?
- Did process-specific code stay out of the universal library?

### Validation commands

- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj --filter "FullyQualifiedName~ProcessTemplate|FullyQualifiedName~WebGl" -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessCanvasSurfaceFactory|FullyQualifiedName~WebGl" -v:minimal`

### Corrective trigger

- If this subbundle fails, open `_corrective-scene-contract-and-layout-reset` before continuing downstream.

### Corrective template

- `subbundles/_corrective-scene-contract-and-layout-reset`

### Repository touchpoints (relative)

- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEditorModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasCatalog.cs`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasBranching.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessDependencyCompatibilityBridge.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplatePackLoader.cs`
- `Templates/Processes/manifest.json`

### Notes

- Treat this subbundle as an isolated execution slice. Do not continue into later numbered work during the same pass.
- Update `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md` as soon as this subbundle either passes, blocks, or triggers a corrective path.
