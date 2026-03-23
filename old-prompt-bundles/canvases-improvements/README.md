# Canvas Improvements Package

This folder is the implementation package for bringing the `CanDoItAll` project structure editor and prompt wizard editor up to the same interaction, layout, and visual standard as the `zyphonote-web` learning builder canvas.

## Scope

This package covers documentation only. No production code is changed here.

The target outcome is:

1. `ProjectStructurePage` reaches functional and visual parity with the richer learning builder canvas.
2. `PromptFactoryPage` stops being a list-first flow editor and becomes a canvas-first editor using the same workbench system.
3. Both editors share one visual system, one canvas interaction contract, and one reusable implementation foundation.

## Primary sources analyzed

Reference implementation in `zyphonote-web`:

- `C:\repositories\zyphonote-web\src\account-learning-builder.php`
- `C:\repositories\zyphonote-web\src\assets\js\zy-learning-builder-page.js`
- `C:\repositories\zyphonote-web\src\assets\js\zy-learning-pack-canvas.js`
- `C:\repositories\zyphonote-web\src\assets\js\zy-canvas-workbench.js`
- `C:\repositories\zyphonote-web\src\input.css`
- `C:\repositories\zyphonote-web\src\account-playlists.php`
- `C:\repositories\zyphonote-web\src\assets\js\zy-playlist-builder-page.js`

Current `CanDoItAll` targets:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Components\ProjectStructureCanvas.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\wwwroot\js\workbenchInterop.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\FactoryDomain.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\PromptFactoryService.cs`

Screenshot evidence reviewed:

- `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\screenshots\Screenshot 2026-03-19 131635.png`
- `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\screenshots\Screenshot 2026-03-19 143934.png`
- `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\screenshots\Screenshot 2026-03-19 144119.png`
- `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\screenshots\Screenshot 2026-03-19 144152.png`
- `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\screenshots\Screenshot 2026-03-19 144204.png`

Existing helpful reference docs already present in this repo:

- `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\README.md`
- `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\behavior\layout-rendering-and-interactions.md`
- `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\guidelines\codex-rebuild-checklist.md`
- `C:\repositories\CanDoItAll\docs\canvas-playlist-builder\rebuild\blazor-jsinterop-component-plan.md`

## Documentation map

- `01-reference-and-gap-analysis.md`
  - What the reference canvas actually does.
  - What the current `CanDoItAll` editors are missing.

- `02-shared-canvas-system-spec.md`
  - The shared architecture, layout system, visual language, interaction contract, and state model both editors must use.

- `03-parity-checklist.md`
  - Detailed feature, layout, visual, state, testing, and QA checklist.

- `04-implementation-plan.md`
  - Ordered execution plan, file ownership guidance, and phase exit criteria.

- `05-sequential-prompts.md`
  - Copy-ready prompts for the separate implementation agent, in the intended order of execution.

- `06-qa-senior-review.md`
  - Final coverage audit, open assumptions, risk watchlist, and readiness signoff.

## Non-negotiable delivery rules

- Do not build two separate canvas systems.
- Do not keep the current `ProjectStructureCanvas` JS and build a different prompt canvas beside it.
- Do not let either editor drift away from the reference chrome, spacing, surface styling, or interaction model.
- Do not reduce the learning builder down to just "a canvas with nodes"; the surrounding inspector, help, chrome, quick-create flows, and persistence are part of the product.
- Do not lose current `CanDoItAll` domain behaviors while adding parity.

## Evidence policy used in these docs

- `Code-confirmed`: directly supported by the analyzed source code.
- `Screenshot-confirmed`: directly visible in the provided screenshots.
- `Inference`: a necessary implementation conclusion drawn from code plus screenshots. These are explicitly called out where relevant.
