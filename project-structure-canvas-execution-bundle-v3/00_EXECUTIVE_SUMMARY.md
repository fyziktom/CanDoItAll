# Executive summary

## Bottom line

The previous bundle moved the code in the right direction, but it did **not** finish the job.

The most important facts today are:

1. `ProjectStructurePage` is **still not running as a real canvas scene renderer**.
2. The highest-cost paths are still:
   - DOM/SVG scene composition,
   - eager persistence in shared-canvas callbacks,
   - full surface reloads after common mutations,
   - incomplete overlay input ownership,
   - maintainability problems caused by very large JS, CSS, and Razor files.
3. The toolbox is still not production-ready:
   - browser expand/collapse behavior is not proven,
   - current item rows are still two-line,
   - there is no proper tooltip-driven description model,
   - layout does not match the compact Visual Studio toolbox pattern.

## What must happen now

### 1) Move the runtime scene to real canvas
The runtime workbench should become a real multi-layer canvas scene with:
- device-pixel-ratio-aware canvases,
- geometry-based hit testing,
- dirty redraw scheduling,
- viewport culling,
- HTML overlays only where they still make sense.

### 2) Keep the right ownership split
- **JS**: renderer, hit testing, drag loop, pan/zoom, minimap, frame drawing, node drawing, hot-zones.
- **C#**: typed surface/domain models, adapters, create/edit/delete actions, service calls, committed persisted state.
- **HTML/Blazor**: toolbox, windows, dialogs, editors, accessibility mirror, context menus.

### 3) Reorganize CanvasLib before the renderer grows again
`CanvasLib` needs a clearer structure:
- runtime workbench code,
- shared helpers,
- preview boundary components,
- calendar code,
- generated asset outputs,
- centralized asset include helpers.

### 4) Split the monoliths
Current file sizes are a real maintenance risk:
- `canvasWorkbenchInterop.js` — 6648 lines
- `canvas-workbench.css` — 4324 lines
- `ProjectStructurePage.razor` — 1808 lines
- `PromptFactoryPage.razor` — 3212 lines
- `CanvasWorkbench.razor` — 723 lines

### 5) Fix the toolbox properly
Do not paint the toolbox into canvas. That would make the product harder to use and would not address the real dense-scene bottleneck.

Instead:
- keep the toolbox as HTML inside a floating window,
- fix accordion logic,
- make rows compact and single-line,
- use tooltip/title for descriptions,
- isolate overlay input ownership fully,
- add browser and screenshot proof.

## Recommended implementation order

1. Baseline capture and missing browser tests.
2. Overlay isolation and wheel ownership.
3. Write-behind state persistence.
4. Remove reload-after-move.
5. Toolbox functional repair.
6. Toolbox compact VS-like UX.
7. CanvasLib directory reorganization and asset pipeline.
8. Split long files into smaller source fragments.
9. Introduce real canvas stage shell.
10. Move links, minimap, diagnostics, and group frames to canvas.
11. Move node cards to canvas using hot-zones plus HTML overlay escape hatches.
12. Update export and accessibility.
13. Adopt the new renderer on ProjectStructurePage.
14. Validate PromptFactory.
15. Clean up dead or legacy paths.

## Success criteria

This program is successful when all of the following are true:

- runtime scene layers are actually canvas-based,
- toolbox is reliable and compact,
- overlay interactions never leak into canvas handlers,
- dense graphs render with far fewer DOM nodes than today,
- `ProjectStructurePage` and `PromptFactoryPage` still preserve feature parity,
- image export still works,
- accessibility mirror still works,
- benchmark and screenshot evidence prove the renderer upgrade is real.
