# Validation report

## Executed in repository
- Rebuilt the applied template pack copy through project builds so `output/process-template-pack` sidecars propagated into test and app outputs.
- Corrected the definition chrome sidecar mismatch from `process-step.approval` to `process-step.release-approval` in the applied pack and bundle overlay source.
- Re-ran `cdi_process_templates_bundle/tools/validate_process_template_pack.py`.
- Rebuilt the component and MCP process test surfaces.
- Ran targeted component, MCP, and Playwright validation for the affected process-template workflows.
- Ran a broader `dotnet build CanDoItAll.slnx -v:minimal`.

## Validator result
- Process count: 9
- Step count: 54
- Dependency count: 52
- Artifact input count: 20
- Baseline scenario count: 5
- Errors: 0

## .NET validation
- `dotnet build CanDoItAll.slnx -v:minimal`
  Result: succeeded
  Notes: only pre-existing warning output remained (`NU1510` in `CanDoItAll.Mcp.DotNetWatch.csproj`).
- `dotnet test tests/CanDoItAll.Mcp.Processes.Tests/CanDoItAll.Mcp.Processes.Tests.csproj -v:minimal`
  Result: 18 passed
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-build --filter "FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessCanvasChromeCatalogServiceTests" -v:minimal`
  Result: 8 passed
- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~Process_management_canvas_bundle_flows_are_validated_in_browser" -v:minimal`
  Result: 1 passed

## Browser proof artifacts
- `output/playwright/process-management-bundle/01-definition-canvas-toolbar.png`
- `output/playwright/process-management-bundle/02-step-editor-from-toolbox.png`
- `output/playwright/process-management-bundle/03-definition-selection-window.png`
- `output/playwright/process-management-bundle/05-definition-double-click-actions.png`
- `output/playwright/process-management-bundle/06-runtime-selection-window.png`

## Bundle closure notes
- The corrective canvas chrome subbundle is no longer only an explicit debt marker; the definition quick-create and group-context chrome now load from the sidecar catalog through `ProcessCanvasChromeCatalogService`.
- The sidecar source of truth is now aligned with the toolbox pack action ids, including `process-step.release-approval`.
