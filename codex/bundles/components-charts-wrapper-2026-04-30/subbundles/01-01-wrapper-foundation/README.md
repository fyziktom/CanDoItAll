# 01-wrapper-foundation

## Status

- `Completed`

## Objective

Create the `CanDoItAll.Components.Charts` Razor Class Library and the first CanDoItAll-owned chart API over Blazor-ApexCharts.

## Covered Inputs

- N001, N002, N003, N004, N005, N006
- Requirements: R001, R002, R003, R004, R005, R006

## Prerequisites

- Bundle prepared validator passed.
- No previous implementation subbundles required.

## Exact Source References

- `C:\repositories\EnergoApp\Enerooo\Enerooo.UI.BasicComponents\Models\Base\ApexGraphComponentBase.cs`
- `C:\repositories\EnergoApp\Enerooo\Enerooo.UI.BasicComponents\Graphs\ConsumptionBarGraph.razor`
- `C:\repositories\EnergoApp\Enerooo\Enerooo.UI.BasicComponents\Graphs\EnergyPricesGraph.razor`
- `C:\repositories\EnergoApp\Enerooo\Enerooo.UI.BasicComponents\Graphs\HouseLast24CombinedGraph.razor`
- `C:\repositories\EnergoApp\Enerooo\Enerooo.UI.BasicComponents\Graphs\Components\HistoryGraphDrawAreaWithSummaryBlocks.razor`
- `C:\repositories\Blazor-ApexCharts\README.md`
- `C:\repositories\Blazor-ApexCharts\src\Blazor-ApexCharts\ApexChart.razor`
- `C:\repositories\Blazor-ApexCharts\src\Blazor-ApexCharts\ApexChart.razor.cs`
- `C:\repositories\Blazor-ApexCharts\src\Blazor-ApexCharts\Series\ApexPointSeries.cs`
- `C:\repositories\Blazor-ApexCharts\src\Blazor-ApexCharts\Internal\JSLoader.cs`
- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\CanDoItAll.Components.Sandbox.csproj`

## Deliverables

- New `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Charts` project.
- Public CanDoItAll chart component/model API.
- Internal ApexCharts adapter implementation.
- `AddCanDoItAllCharts()` service registration.
- `ChartsHeadAssets` host asset component.
- Solution and sandbox/test project references as needed.

## Dependency Impact

- The sandbox page depends on this wrapper.
- If this API leaks direct Apex component usage, the sandbox would not prove future replaceability.
- If DI/assets are wrong, later browser proof will show blank charts.

## Validation Depth

- Critical foundation: build proof plus API boundary review.

## Implementation Steps

1. Create the charts RCL project with `Blazor-ApexCharts` package dependency.
2. Add public CanDoItAll chart models/enums with no Apex-specific consumer requirement.
3. Implement `CdaChart` to translate public models to Apex options and series.
4. Add `ChartsHeadAssets` and `AddCanDoItAllCharts()`.
5. Add the project to `CanDoItAll.slnx`.
6. Add sandbox and test project references needed for downstream phases.
7. Build the charts RCL and record results.

## Scope Exceptions

- This phase does not add sandbox examples; that is owned by `02-02-sandbox-chart-examples`.
- This phase does not implement every ApexCharts option; only common operational chart patterns required by the request.

## Do Not Do

- Do not copy EnergoApp domain services, DTOs, Radzen controls, or app copy.
- Do not require sandbox consumers to write `<ApexChart>` or `<ApexPointSeries>`.
- Do not replace the existing BaseLib SVG `Chart.razor`.
- Do not use the local Blazor-ApexCharts clone as a project reference.

## Acceptance Checklist

- New project exists and builds.
- Public wrapper models/components are in `CanDoItAll.Components.Charts`.
- Host registration is available through `AddCanDoItAllCharts()`.
- Asset inclusion is available through a wrapper component.
- No downstream sandbox page is forced to reference Apex components directly.

## Proof Required

- `dotnet build src/CanDoItAll.Components.Charts/CanDoItAll.Components.Charts.csproj` passed on 2026-04-30.
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-restore --no-build --filter "FullyQualifiedName~ChartsWrapperTests"` passed on 2026-04-30.
- `dotnet build src/CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj` passed on 2026-04-30 after adding the project reference.
- API boundary review: public consumer models/components are CanDoItAll-owned; Apex types are internal to the adapter component/factory and DI extension.

## Browser Validation Logging

- N/A for this subbundle alone; browser proof occurs when the sandbox consumes the wrapper in `02-02-sandbox-chart-examples`.

## Progression Gate

- Passed on 2026-04-30: charts RCL builds, targeted adapter tests pass, sandbox can reference the project, and public consumer API is CanDoItAll-owned.

## Suggested Agent Prompt

```text
Implement the wrapper foundation only. Create the charts RCL, CanDoItAll-owned models/components, DI registration, and asset component. Do not build the sandbox examples yet.
```
