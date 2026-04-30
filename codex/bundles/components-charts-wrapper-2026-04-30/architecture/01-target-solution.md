# Target Solution

## Library Boundary

Create `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Charts\CanDoItAll.Components.Charts.csproj` as a Razor Class Library targeting `net10.0`.

The public API should live in namespace `CanDoItAll.Components.Charts` and use CanDoItAll-owned names:

- `CdaChart.razor`: main chart wrapper component.
- `CdaChartSeries`: consumer-facing series model.
- `CdaChartPoint`: consumer-facing point model.
- `CdaChartType`, `CdaChartAxisType`, `CdaChartCurve`, `CdaChartLegendPosition`: CanDoItAll enums.
- `ChartsHeadAssets.razor`: host asset component for package CSS.
- `ServiceCollectionExtensions.AddCanDoItAllCharts()`: host registration.

Implementation may use `ApexCharts` internally, but page consumers should not need `ApexChart`, `ApexPointSeries`, `ApexChartOptions<T>`, or `SeriesType`.

## Adapter Responsibilities

- Translate CanDoItAll chart enums and series models to `ApexCharts.SeriesType`, `ApexCharts.XAxisType`, options, fill, stroke, legend, tooltip, grid, toolbar, and label settings.
- Build a fresh `ApexChartOptions<CdaChartPoint>` per component instance.
- Render `ApexPointSeries<CdaChartPoint>` internally.
- Call `UpdateSeriesAsync` and `UpdateOptionsAsync` after render when parameters change, following the working EnergoApp pattern.
- Keep empty state rendering inside the wrapper so consumers do not need library-specific no-data handling.

## Sandbox Page

Add a new sandbox group/page route, expected route:

- `/groups/charts`

The page should use the existing catalog frame and shared layout components. It should present common examples using generated sample data:

- Pie/donut share by source.
- Single line datetime trend.
- Multi-line datetime trend.
- Filled area chart with unit formatting.
- Color-tuned series with labels or point colors.
- Operational summary context inspired by EnergoApp screenshots.

## Asset And DI Flow

Host setup should be:

```csharp
builder.Services.AddCanDoItAllCharts();
```

Host markup should be:

```razor
<ChartsHeadAssets />
```

The wrapper owns mapping those calls to Blazor-ApexCharts service registration and static CSS. Blazor-ApexCharts JS loads dynamically through its own package interop.
