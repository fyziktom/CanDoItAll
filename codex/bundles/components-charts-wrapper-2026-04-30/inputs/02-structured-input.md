# Structured Input

## Objectives

- Analyze working EnergoApp ApexCharts usage and the local Blazor-ApexCharts package source.
- Introduce a new Razor Class Library: `CanDoItAll.Components.Charts`.
- Keep consumers insulated from direct ApexCharts component usage where practical, so the implementation can later move to another chart library.
- Add a new sandbox page/group with common chart examples.
- Demonstrate pie, single-line, multi-line, color tuning, filled area under-line color, labels, legends, units, datetime axes, and operational summary context.

## Hard Constraints

- Use the `candoitall-bundle-workflow` process and keep bundle proof updated.
- Use the external `Blazor-ApexCharts` package as the implementation adapter.
- Do not copy EnergoApp application-specific DTOs, services, Radzen controls, Czech copy, or domain logic into CanDoItAll.
- Use CanDoItAll shared layout components such as `PageScaffold`, `Grid`, `Stack`, and `SectionCard` in the sandbox.
- The sandbox page must be browser-validated with real rendered charts, not only compiled.

## Assumptions

- The wrapper should target `.NET 10` to match the current component libraries and sandbox.
- A compact first wrapper is preferable to exposing the whole Apex options surface; an escape hatch can be added later only when real consumers need it.
- Package-level service registration should be hidden behind `AddCanDoItAllCharts()`.
- Static CSS assets should be exposed through a tiny `ChartsHeadAssets` component so hosts do not need to know package asset paths.

## Validation Expectations

- Bundle prepared gate passes before code implementation.
- Each subbundle entry and closure gate is explicitly recorded.
- `dotnet build` for the new project and sandbox succeeds.
- Targeted component/model tests cover option generation and non-Apex public contracts.
- Playwright/browser proof confirms the new sandbox route renders nonblank chart SVG/canvas DOM on desktop and mobile widths.
- Screenshot review answers readability, overlap, hierarchy, spacing, and visual-system fit questions.
