# Current State

## Styling Pipeline

- Tailwind v4 compiles from `C:\repositories\CanDoItAll\Tailwind\input.css` into `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\wwwroot\css\output.css`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\App.razor` and `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\App.razor` both load the compiled BaseLib stylesheet directly from `_content/CanDoItAll.Components.BaseLib/css/output.css`.
- This is already the correct packaging shape for a NuGet-delivered visual system. The missing piece is a semantic variable contract that can be overridden downstream.

## Theme-System Gap

- Non-canvas shared UI has tone names such as `cda-button--tone-primary`, but those selectors are backed by hard-coded palette utilities in `C:\repositories\CanDoItAll\Tailwind\controls\buttons.css`.
- There is no meaningful non-canvas runtime theme hook today. The inventory found only four non-canvas files with theme-related hits, and none of them represent a shared app-level theme contract.
- CanvasLib already uses a typed token pack in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Core\CanvasThemeTokenPack.cs`. That pattern proves the repo accepts token-driven styling, but it is currently isolated to canvas surfaces.

## Prefix Fragmentation

- The inventory workbook recorded `832` `zy-*` references and `801` `cda-*` references across the scanned solution files.
- `cad-*` is effectively absent today, which means prefix stabilization is a real migration, not a cosmetic rename.
- The heaviest non-canvas prefix concentrations currently live in BaseLib tabs, Tailwind component files, and module pages such as Resources, Settings, Prompt Gallery, and Project modal/detail surfaces.

## Hard-Coded Palette Hotspots

- `C:\repositories\CanDoItAll\Tailwind\controls\buttons.css` is the clearest shared hotspot because it contains both the canonical tone API and raw color utilities for primary, secondary, success, info, warning, and danger.
- `C:\repositories\CanDoItAll\Tailwind\navigation\workbench-shell.css`, `C:\repositories\CanDoItAll\Tailwind\navigation\tabs.css`, and `C:\repositories\CanDoItAll\Tailwind\navigation\treeview.css` contain shared chrome surfaces with embedded colors and radii.
- BaseLib components such as `Button`, `Badge`, `StatusBadge`, `Alert`, `TextBlock`, and several card/input primitives still inline palette utilities in Razor markup or switch expressions instead of using theme variables.
- Consuming routes such as `ResourcesPage`, `PromptGalleryPage`, `ProjectModalHost`, `SettingsPage`, `MainLayout`, and `ProjectCalendarPage` still encode palette utilities directly in page markup.

## Reusable Strengths

- BaseLib already exposes most of the primitives that should carry the theme contract: buttons, badges, cards, forms, lists, feedback, tabs, treeview, and page headers.
- The broad style-unification bundle at `C:\repositories\CanDoItAll\solution-style-unification-bundle-v1` already established a useful taxonomy: buttons, forms, typography, layout, feedback, and navigation.
- Because BaseLib styles are already centralized into one compiled asset, a semantic token layer can propagate widely once those primitives stop hard-coding palette values.

## Immediate Scope Boundaries

- The bundle targets the shared non-canvas UI system built around BaseLib and its consuming modules.
- CanvasLib is not the primary migration surface for this bundle, but its token-pack design is an architectural reference and its existing `zy-*` footprint is a known future cleanup area.
- The current bundle must confirm future Zyphonote reuse, but it does not need to refactor Zyphonote apps yet.
