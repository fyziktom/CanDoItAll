# Corrective scene-contract and layout reset

## Status

- Blocked

## Objective

- Repair failures where the projected process scene became visually confusing, semantically inconsistent with current process IDs/categories, or too coupled to the sandbox.

## Covered Inputs

- `RQ-09`
- `RQ-10`
- `RQ-12`
- `RQ-13`
- `RQ-19`

## Prerequisites

- Prepared bundle readiness gate passed.
- No downstream implementation may begin until this subbundle owns the active work item.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessCanvasCatalog.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessCanvasBranching.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs
- C:/repositories/CanDoItAll/Templates/Processes/manifest.json
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/ProcessCanvasSurfaceFactoryTests.cs

## Deliverables

- A focused corrective change set that repairs depth rules, label strategy, or process-semantic projection.
- Updated readability notes and screenshots proving the reset helped rather than shuffled the problem.
- Fresh Gate B memo after the repair.

## Dependency Impact

- Automation hardening remains blocked until the scene is worth proving.

## Validation Depth

- Critical corrective
- Focused adapter tests + refreshed screenshots

## Implementation Steps

1. Identify whether the failure comes from bad depth rules, poor label projection, wrong process mapping, or sandbox leakage.
2. Refactor the scene contract and layout policy to restore deterministic readable behavior.
3. Refresh screenshots, semantic tests, and rerun Gate B.


## Do Not Do

- Do not continue if screenshots still show occluded or confusing labels on the representative templates.
- Do not treat free-form camera tricks as a substitute for a good default scene layout.

## Acceptance Checklist

- Representative templates are readable enough to justify final proof work.
- Process semantics and IDs are consistent again.
- Gate B can be rerun on fresh evidence.

## Proof Required

- Run the focused adapter/sandbox tests.
- Refresh screenshots for the simple, medium, and dense templates.
- Update the corrective memo and rerun Gate B.
- Validation commands to run for this subbundle:
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~WebGl|FullyQualifiedName~ProcessCanvasSurfaceFactory" -v:minimal`

## Browser Validation Logging

- Re-capture the medium and dense template views and answer the readability questions explicitly.

## Progression Gate

- Automation and closure work remain blocked until Gate B passes on fresh proof after this corrective.

## Suggested Agent Prompt

```text
Execute only the corrective scene-contract and layout reset. Repair the guided 3D scene semantics or readability problems, refresh screenshots and adapter proof, rerun Gate B, and keep downstream work blocked until the gate explicitly passes.
```

## Preserved Bundle Notes

### Review questions

- Did the corrective actually improve readability?
- Are process IDs and categories still trustworthy?
- Can Gate B now pass honestly?

### Validation commands

- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~WebGl|FullyQualifiedName~ProcessCanvasSurfaceFactory" -v:minimal`

### Corrective trigger

- This subbundle is itself a corrective playbook.

### Corrective template

- Not applicable.

### Repository touchpoints (relative)

- `src/CanDoItAll.Modules.Processes/ProcessCanvasCatalog.cs`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasBranching.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs`
- `Templates/Processes/manifest.json`
- `tests/CanDoItAll.Tests.Components/ProcessCanvasSurfaceFactoryTests.cs`

### Notes

- Treat this subbundle as an isolated execution slice. Do not continue into later numbered work during the same pass.
- Update `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md` as soon as this subbundle either passes, blocks, or triggers a corrective path.
