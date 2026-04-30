# Current State

## EnergoApp ApexCharts Usage

- `C:\repositories\EnergoApp\Enerooo\Enerooo.UI.BasicComponents\Models\Base\ApexGraphComponentBase.cs` centralizes Apex defaults: toolbar controls, zoom, disabled animation, datetime axes, tooltip formatters, grid row shading, bar/area switching, and post-render `UpdateSeriesAsync` / `UpdateOptionsAsync`.
- `C:\repositories\EnergoApp\Enerooo\Enerooo.UI.BasicComponents\Graphs\ConsumptionBarGraph.razor` shows the minimum working pattern: `ApexChart<TItem>`, `ApexPointSeries<TItem>`, typed `XValue` and `YValue`, datetime axis, unit-aware tooltip, and a chart-type toggle.
- `C:\repositories\EnergoApp\Enerooo\Enerooo.UI.BasicComponents\Graphs\EnergyPricesGraph.razor` shows area charts, color palettes, threshold coloring, `FillTo = Origin`, and zero-line behavior for prices.
- `C:\repositories\EnergoApp\Enerooo\Enerooo.UI.BasicComponents\Graphs\HouseLast24CombinedGraph.razor` and `Components\HistoryGraphDrawAreaWithSummaryBlocks.razor` show multi-series line/area charts with stable per-series colors, legends, summary context, and device toggles.
- EnergoApp has useful patterns but also app-specific dependencies: Radzen controls, Energo DTOs, device services, Czech labels, and house/flat domain concepts. The wrapper must harvest chart behavior, not copy product code.

## Blazor-ApexCharts Package Shape

- `C:\repositories\Blazor-ApexCharts\README.md` documents `services.AddApexCharts()` and `_Imports.razor` usage with `@using ApexCharts`.
- `C:\repositories\Blazor-ApexCharts\src\Blazor-ApexCharts\ApexChart.razor` renders a div container and cascades the chart to child series.
- `C:\repositories\Blazor-ApexCharts\src\Blazor-ApexCharts\ApexChart.razor.cs` loads JS through `JSLoader.LoadAsync`, prepares chart options, renders on first post-render pass, and supports `UpdateSeriesAsync` and `UpdateOptionsAsync`.
- `C:\repositories\Blazor-ApexCharts\src\Blazor-ApexCharts\Internal\JSLoader.cs` dynamically imports `_content/Blazor-ApexCharts/js/blazor-apexcharts.js`.
- `C:\repositories\Blazor-ApexCharts\src\Blazor-ApexCharts\wwwroot\css\apexcharts.css` is the chart CSS asset; the wrapper host should include it without making product pages reference package paths directly.
- `ApexChartOptions<TItem>` instances must not be shared between chart instances. The wrapper must build a fresh options object for each `CdaChart`.

## CanDoItAll Component/Sandbox State

- CanDoItAll component libraries target `net10.0`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\DataVisualization\Chart.razor` is a simple SVG line chart, not an ApexCharts adapter; new work should avoid naming collisions with `Chart`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\App.razor` is an interactive server Blazor shell with BaseLib CSS and CanvasLib assets already loaded.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\SandboxCatalogRegistry.cs` drives sandbox navigation and examples; a chart group should be added there.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\DataDisplay.razor` provides the local page pattern: `CatalogPageFrame`, `SectionCard`, `Grid`, scenarios, notes, and shared component usage.
- Components MCP lookup confirmed `PageScaffold`, `Grid`, and `Stack` are the expected shared layout primitives for sandbox page structure.
