# Dedicated WebGL sandbox and template switching

## Status

- Completed

## Objective

- Add the dedicated WebGL sandbox project, surface the projected templates, and provide camera/view controls for human concept review.

## Covered Inputs

- `IN-07`
- `IN-08`
- `IN-09`
- `RQ-11`
- `RQ-12`
- `RQ-13`
- `RQ-14`

## Prerequisites

- `05-process-template-projection-and-2_5d-scene-adapter`

## Exact Source References

- C:/repositories/CanDoItAll/CanDoItAll.slnx
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.Sandbox/Program.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.Sandbox/Components/Pages/Canvas.razor
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.Sandbox/SandboxCatalogRegistry.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj
- C:/repositories/CanDoItAll/Templates/Processes/manifest.json

## Deliverables

- New dedicated sandbox project for the WebGL concept with its own routes/startup.
- Template switching between representative template processes.
- Camera/view preset controls and a clean review surface for screenshots.

## Dependency Impact

- Interaction and automation work depend on this sandbox route existing and being stable.
- If the sandbox pulls in production workspace concerns, the concept branch boundary is compromised.

## Validation Depth

- High with mandatory browser proof
- Build + targeted browser route screenshots

## Implementation Steps

1. Create the dedicated sandbox project and wire it into the solution.
2. Reference the new WebGL library and the existing process-template services needed for projection.
3. Build a focused concept page with template selector, camera presets, fit view, and reset controls.
4. Load the representative templates into the scene and capture first-pass screenshots.


## Do Not Do

- Do not reuse the production `ProcessWorkspace` page as the sandbox host.
- Do not add database-backed persistence.
- Do not hide template switching behind debug-only code.

## Acceptance Checklist

- A dedicated sandbox project exists and runs.
- The sandbox can switch between representative templates.
- First-pass screenshots show a readable concept route for simple, medium, and dense scenes.

## Proof Required

- Build the solution.
- Capture screenshots for at least three template scenarios and two viewport sizes.
- Record any readability failures or occlusion problems honestly.
- Validation commands to run for this subbundle:
- `dotnet build CanDoItAll.slnx -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~WebGlSandboxSmoke" -v:minimal`

## Browser Validation Logging

- Route: proposed `/webgl/process-workbench` or equivalent inside the dedicated sandbox project.
- Viewports: `1600x900`, `1366x768`, and `430x932`.
- Actions: open simple/medium/dense templates, fit view, switch camera presets, capture screenshots.
- Review questions: are labels readable, is the scene clipped, does depth help rather than confuse, and does narrow view still orient the reviewer quickly?

## Progression Gate

- Interaction work may continue only after the dedicated sandbox loads real templates and first-pass screenshots prove the scene is legible enough to justify authoring features.

## Suggested Agent Prompt

```text
Implement only subbundle 06. Add the dedicated WebGL sandbox project, wire real template projection into it, expose template switching and camera/view presets, capture screenshot proof for representative templates, and stop before authoring interactions.
```

## Preserved Bundle Notes

### Review questions

- Does the dedicated sandbox stay concept-focused and isolated from the production workspace?
- Do the representative templates cover a meaningful range of complexity?
- Do screenshots suggest the guided 3D scene improves or at least clarifies some dense cases?

### Validation commands

- `dotnet build CanDoItAll.slnx -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~WebGlSandboxSmoke" -v:minimal`

### Corrective trigger

- If this subbundle fails, open `_corrective-scene-contract-and-layout-reset` before continuing downstream.

### Corrective template

- `subbundles/_corrective-scene-contract-and-layout-reset`

### Repository touchpoints (relative)

- `CanDoItAll.slnx`
- `src/CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj`
- `src/CanDoItAll.Components.Sandbox/Program.cs`
- `src/CanDoItAll.Components.Sandbox/Components/Pages/Canvas.razor`
- `src/CanDoItAll.Components.Sandbox/SandboxCatalogRegistry.cs`
- `src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `Templates/Processes/manifest.json`

### Notes

- Treat this subbundle as an isolated execution slice. Do not continue into later numbered work during the same pass.
- Update `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md` as soon as this subbundle either passes, blocks, or triggers a corrective path.
