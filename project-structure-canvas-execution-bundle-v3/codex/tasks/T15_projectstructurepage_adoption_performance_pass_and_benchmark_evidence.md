# T15 — ProjectStructurePage adoption, performance pass, and benchmark evidence

## Phase
P3

## Goal
Make ProjectStructurePage the default adopter of the real canvas renderer, reduce residual DOM-heavy support surfaces where appropriate, and prove the result with benchmark evidence and screenshots.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T03, T04, T05, T10, T11, T12, T13, T14

## Primary files
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.*.cs`
- `src/CanDoItAll.Components.Sandbox/Components/Pages/CanvasBenchmark.razor`
- `src/CanDoItAll.Components.Sandbox/wwwroot/js/canvasBenchmarkPage.js`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

## Feature IDs that must remain green
F01, F02, F03, F04, F05, F06, F07, F08, F09, F10, F11, F12, F13, F14, F15, F16, F17, F18, F19, F20, F21, F22, F23, F24, F25, F26, F27, F28, F29, F30, F31, F32, F35, F36, F37, F38, F40

## Implementation checklist
- Adopt the new renderer on ProjectStructurePage and remove remaining runtime DOM/SVG scene reliance.
- Reduce residual support-surface cost where safe without moving rich UI into canvas.
- Use CanvasBenchmark and runtime counters to prove the adoption win.
- Collect final ProjectStructure screenshots for dense graphs and key workflows.

## Validation
- ProjectStructurePage full browser regression suite passes with the real canvas renderer enabled.
- DOM node count inside the stage drops materially versus baseline.
- Benchmark and diagnostics evidence show a meaningful improvement for dense scenes.

## Done when
- ProjectStructurePage becomes the primary tuned adopter of the new renderer, as originally intended.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
