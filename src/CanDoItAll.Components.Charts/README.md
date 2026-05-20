# CanDoItAll.Components.Charts

## Purpose

Shared Razor chart wrapper over Blazor ApexCharts with typed CanDoItAll chart models, palette options, axes, curves, legends, and head assets.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Components.Charts/CanDoItAll.Components.Charts.csproj
```

## References

Project references:

- None

Framework references:

- None

Direct package references:

- `Blazor-ApexCharts (6.1.0)`
- `Microsoft.AspNetCore.Components.Web (10.0.4)`

## Architecture Notes

Use this library when product modules need charts instead of binding directly to ApexCharts. Keep `CdaChart`, `CdaChartOptions`, `CdaChartSeries`, and `CdaChartPoint` as the typed boundary so modules can share chart behavior without copying JavaScript or package-specific options.

## Related Docs

- Repository overview: `README.md` at the repo root
- Shared components: `docs/ui-shared-components/README.md`
