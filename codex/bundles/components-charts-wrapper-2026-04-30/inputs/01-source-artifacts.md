# Source Artifacts

## User-Supplied Local Paths

| Artifact | Path | Purpose |
| --- | --- | --- |
| EnergoApp graph components | `C:\repositories\EnergoApp\Enerooo\Enerooo.UI.BasicComponents\Graphs` | Working reference implementation for Blazor-ApexCharts usage patterns. |
| EnergoApp Apex base class | `C:\repositories\EnergoApp\Enerooo\Enerooo.UI.BasicComponents\Models\Base\ApexGraphComponentBase.cs` | Reference for shared Apex options, toolbar, axes, fill/stroke, tooltip, and update lifecycle. |
| Cloned Blazor-ApexCharts source | `C:\repositories\Blazor-ApexCharts` | Reference for package setup, public component API, service registration, static assets, and chart update behavior. |
| Screenshot copy: modal bar chart | `C:\repositories\CanDoItAll\codex\bundles\components-charts-wrapper-2026-04-30\inputs\screenshots\Screenshot 2026-04-25 085501.png` | Visual target for dense datetime bar chart, toolbar, legend, and modal-style sizing. |
| Screenshot copy: consumption area chart | `C:\repositories\CanDoItAll\codex\bundles\components-charts-wrapper-2026-04-30\inputs\screenshots\Screenshot 2026-04-25 085436.png` | Visual target for filled area chart, summary rail, units, and datetime labels. |
| Screenshot copy: navigation route with Graph item | `C:\repositories\CanDoItAll\codex\bundles\components-charts-wrapper-2026-04-30\inputs\screenshots\Screenshot 2026-04-25 085559.png` | Context that EnergoApp treats graphs as a first-class surface, not only inline cards. |
| Screenshot copy: multi-series overview graph | `C:\repositories\CanDoItAll\codex\bundles\components-charts-wrapper-2026-04-30\inputs\screenshots\Screenshot 2026-04-25 085323.png` | Visual target for multi-series area/line, color tuning, legend, toolbar, and stacked information density. |
| Screenshot copy: dashboard metrics | `C:\repositories\CanDoItAll\codex\bundles\components-charts-wrapper-2026-04-30\inputs\screenshots\Screenshot 2026-04-25 085635.png` | Context for summary metrics that may accompany charts in the sandbox. |

## CanDoItAll Source References

| Artifact | Path | Purpose |
| --- | --- | --- |
| Sandbox app project | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\CanDoItAll.Components.Sandbox.csproj` | Add project reference and package service registration consumer. |
| Sandbox app shell | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\App.razor` | Add chart head assets if wrapper exposes them. |
| Sandbox catalog registry | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\SandboxCatalogRegistry.cs` | Add chart group and example routes. |
| Existing sandbox page pattern | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\DataDisplay.razor` | Pattern for catalog page frame, scenarios, and shared BaseLib layout usage. |
| Existing shared SVG chart | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\DataVisualization\Chart.razor` | Existing non-Apex chart surface to avoid name collisions and clarify the new wrapper boundary. |
| Shared component layout primitives | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Layout\PageScaffold.razor`, `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Layout\Grid.razor` | Use shared layout components in the sandbox instead of ad-hoc page scaffolding. |
| Solution file | `C:\repositories\CanDoItAll\CanDoItAll.slnx` | Add the new charts project. |
