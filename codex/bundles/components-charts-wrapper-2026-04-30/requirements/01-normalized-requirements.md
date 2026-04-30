# Normalized Requirements

| ID | Requirement | Validation |
| --- | --- | --- |
| R001 | Analyze EnergoApp graph components and preserve reusable chart patterns without copying app-specific domain code. | Current-state notes cite exact EnergoApp files and wrapper behavior reflects toolbar, datetime, tooltip, legend, area/bar/line, and color patterns. |
| R002 | Analyze the cloned `Blazor-ApexCharts` source for package service registration, asset loading, typed component model, and lifecycle. | Current-state notes cite package source files; wrapper project uses correct registration and asset inclusion. |
| R003 | Add a new `CanDoItAll.Components.Charts` Razor Class Library. | New project exists, is in `CanDoItAll.slnx`, targets `net10.0`, builds, and is referenced by sandbox. |
| R004 | Keep product/sandbox consumers behind a CanDoItAll chart contract rather than direct ApexCharts component markup. | Public sandbox examples use `CdaChart`/CanDoItAll chart models; no sandbox page uses `ApexChart` or `ApexPointSeries` directly. |
| R005 | Hide ApexCharts DI behind CanDoItAll registration. | `AddCanDoItAllCharts()` exists and sandbox calls it; direct `AddApexCharts()` is not required in host code. |
| R006 | Provide chart asset inclusion without leaking raw package asset paths throughout pages. | `ChartsHeadAssets` or equivalent wrapper asset component exists and sandbox shell uses it. |
| R007 | Support common chart cases: pie, single line, multiple lines, filled area, color tuning, labels, units, datetime/category axes, legend, and toolbar. | Sandbox page renders each case with sample data and visible controls/summary context. |
| R008 | Use shared BaseLib layout primitives for the sandbox page. | Sandbox route uses `CatalogPageFrame`, `Grid`, `Stack`/`SectionCard` style shared components rather than isolated page scaffolding. |
| R009 | Validate with build/test and real browser proof. | Build/test commands and Playwright screenshots/DOM assertions are recorded in `reviews/01-execution-report.md`. |
| R010 | Keep bundle state synchronized with implementation and raw-note closure. | Final execution report records subbundle gates, browser analytics, raw note closure, residual risks, and final validator result. |
