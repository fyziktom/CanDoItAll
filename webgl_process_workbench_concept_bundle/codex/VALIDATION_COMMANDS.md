# Validation commands

## Bundle validators

- `python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py webgl_process_workbench_concept_bundle --profile initiative --stage prepared`
- `python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py webgl_process_workbench_concept_bundle --profile initiative --stage completed`

## Executed validation commands

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj -c Release --filter "FullyQualifiedName~ProcessWebGlSandboxSessionTests|FullyQualifiedName~WebGlWorkbenchInteropTests|FullyQualifiedName~ProcessWebGlSceneAdapterTests" -v:minimal`
- `dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj -c Release --filter "FullyQualifiedName~WebGlSandboxSmokeTests" -v:minimal`
- `node tools\webgllib\verify-assets.cjs`
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~WebGlWorkbenchUiStateTests" -v:minimal`
- `dotnet test tests\CanDoItAll.Mcp.Processes.Tests\CanDoItAll.Mcp.Processes.Tests.csproj -c Release --filter "FullyQualifiedName~ProcessTemplate|FullyQualifiedName~WebGl" -v:minimal`

## Browser proof artifacts

- `output/playwright/webgl-sandbox/01-webgl-default-template.png`
- `output/playwright/webgl-sandbox/02-webgl-dense-template.png`
- `output/playwright/webgl-sandbox/03-webgl-semantic-proof.png`
- `output/playwright/webgl-sandbox/04-webgl-route-1366x768.png`
- `output/playwright/webgl-sandbox/05-webgl-route-430x932.png`
