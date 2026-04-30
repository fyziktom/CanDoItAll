# Assumptions And Risks

## Assumptions

- The first public contract can support common operational charts through `CdaChart`, `CdaChartSeries`, `CdaChartPoint`, and option enums rather than exposing the entire ApexCharts API.
- A chart host can register `AddCanDoItAllCharts()` once, and the wrapper will internally call `AddApexCharts()`.
- The sandbox is the correct place for generated sample data because no product module has a real chart consumer yet.
- The wrapper should use package references, not a project reference to `C:\repositories\Blazor-ApexCharts`, so it remains an external-library boundary.

## Critical Path Risks

- If the wrapper exposes Apex-specific types in common consumer parameters, a future library swap would still require product page rewrites. This would invalidate the central objective.
- If `Blazor-ApexCharts` version selection does not restore or does not work with .NET 10, sandbox validation cannot proceed.
- If chart assets are missing from the host page, charts can compile but render blank in the browser.
- If options objects are accidentally shared, multiple charts can leak state or render incorrectly.

## Validation Risks

- bUnit/component tests do not prove Apex JS rendering; browser validation is required.
- Browser screenshots can show a page shell while charts remain blank; validation must check chart DOM/SVG content or Apex-generated markup.
- Mobile width can clip rotated labels, legends, or toolbars even when desktop looks fine.
- The sandbox can drift into decorative dashboard copy. It should stay an operational component proof page.

## Reopen Triggers

- Reopen `01-01-wrapper-foundation` if sandbox implementation needs Apex types in consumer-facing parameters.
- Reopen `01-01-wrapper-foundation` if browser proof shows blank charts or missing assets.
- Reopen `02-02-sandbox-chart-examples` if examples do not cover pie, single-line, multi-line, area fill, color tuning, labels, and units.
- Reopen `02-02-sandbox-chart-examples` if desktop or mobile screenshot review finds unreadable labels, overlap, clipped charts, or incoherent spacing.
- Reopen `03-03-validation-and-closure-proof` if any raw note remains only partially proven without a concrete blocker or follow-up.
