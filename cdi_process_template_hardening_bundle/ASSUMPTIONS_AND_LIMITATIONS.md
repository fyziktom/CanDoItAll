# Assumptions and limitations

## Assumptions used during execution
- The repository working tree was the source of truth for code and template-pack state.
- The older `cdi_process_templates_bundle/apply-manifest.json` remained the truth source for the materialization audit.
- Closure required honest proof from repository state, not only from bundle-overlay contents.

## Limitations that remain explicit
- The validation set was targeted, not a full rerun of every test project in the solution.
- This bundle did not require a fresh Playwright/browser proof pass.
- Existing non-bundle warnings remain visible:
  - `NU1510` in `src/CanDoItAll.Mcp.DotNetWatch/CanDoItAll.Mcp.DotNetWatch.csproj`
  - `ASP0006` in `tests/CanDoItAll.Tests.Components/TabsComponentTests.cs`

## Honesty rule
This bundle may now be described as executed repository remediation, but only with the exact validation boundary recorded in `VALIDATION_REPORT.md`.
