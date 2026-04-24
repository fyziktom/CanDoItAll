# Baseline and renderer decision lock

## Status

- Completed

## Objective

- Establish the current-process and current-canvas baseline, document the renderer strategy, and lock the concept boundaries before any implementation starts.

## Covered Inputs

- `IN-01`
- `IN-02`
- `IN-03`
- `IN-04`
- `IN-11`
- `IN-17`
- `IN-18`
- `RQ-01`
- `RQ-02`
- `RQ-03`
- `RQ-09`
- `RQ-13`
- `RQ-17`
- `RQ-21`

## Prerequisites

- Prepared bundle readiness gate passed.
- No downstream implementation may begin until this subbundle owns the active work item.

## Exact Source References

- C:/repositories/CanDoItAll/CanDoItAll.slnx
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchSurface.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs
- C:/repositories/CanDoItAll/Templates/Processes/manifest.json
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs

## Deliverables

- Prepared-stage analysis documents and workbook baseline are finalized.
- Renderer choice, perspective-first 3D default, and concept non-goals are explicitly recorded.
- Representative template set and proof scenarios are locked before implementation.

## Dependency Impact

- Every later subbundle depends on the boundary decisions made here.
- If the concept scope leaks into the production workspace here, all later proof becomes misleading.

## Validation Depth

- Critical preparation only
- Bundle validator + repo-backed analysis consistency review

## Implementation Steps

1. Audit the current CanvasLib workbench, Processes canvas surface factory, template projection services, and current Playwright hooks.
2. Record the explicit decision to use a thin Blazor wrapper over a JS-owned WebGL runtime with a deterministic center-lane 3D process scene.
3. Choose the initial template set for simple, medium, and dense sandbox scenarios.
4. Finalize the workbook, traceability, and phase plan before implementation begins.


## Do Not Do

- Do not start coding the new library or sandbox in this subbundle.
- Do not assume a full 3D free-camera experience is the default authoring mode.
- Do not promise persistence or production route replacement.

## Acceptance Checklist

- The baseline docs are specific to the current repository and not generic WebGL advice.
- The renderer and boundary decisions are explicit enough that Codex can implement without reopening fundamental questions.
- The template set and proof expectations are locked.

## Proof Required

- Run the prepared-stage bundle validator.
- Review the current repository touchpoints listed in the audit docs.
- Confirm workbook presence and cross-links from requirements/traceability docs.
- Validation commands to run for this subbundle:
- `python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py webgl_process_workbench_concept_bundle --profile initiative --stage prepared`

## Browser Validation Logging

- No new browser capture is required in this preparation-only subbundle.
- Log the future proof routes and viewports in the execution report placeholders.

## Progression Gate

- Downstream work may continue only after the renderer strategy, perspective-first 3D default, sandbox isolation rule, and representative template set are explicitly accepted.

## Suggested Agent Prompt

```text
Implement only subbundle 01. Finalize the repo-backed baseline, lock the renderer and the guided 3D concept direction, confirm the representative template set, update the workbook and traceability, validate the prepared bundle, and stop before any production code changes.
```

## Preserved Bundle Notes

### Review questions

- Is the WebGL plan clearly a concept branch rather than a hidden production rewrite?
- Is the guided 3D recommendation justified by current repository constraints?
- Are the template and proof scenarios representative enough to de-risk later phases?

### Validation commands

- `python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py webgl_process_workbench_concept_bundle --profile initiative --stage prepared`

### Corrective trigger

- If this subbundle fails, open `_corrective-renderer-boundary-reset` before continuing downstream.

### Corrective template

- `subbundles/_corrective-renderer-boundary-reset`

### Repository touchpoints (relative)

- `CanDoItAll.slnx`
- `src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor`
- `src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchSurface.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasSurfaceFactory.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateProjectionService.cs`
- `Templates/Processes/manifest.json`
- `tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

### Notes

- Treat this subbundle as an isolated execution slice. Do not continue into later numbered work during the same pass.
- Update `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md` as soon as this subbundle either passes, blocks, or triggers a corrective path.
