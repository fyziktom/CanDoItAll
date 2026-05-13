# Tests API And Browser Proof

## Status

- `Ready`

## Objective

- Complete unit/integration/component/browser proof and regression matrix.

## Success Criteria

- Unit, integration, component, and browser tests cover plugin MVP and future-facing seams.
- API and UI proof is captured and reviewed.
- Core workflow/built-in executor behavior is not regressed.
- Execution report contains enough evidence for final review.

## Covered Inputs

- `R020`
- `R021`
- `R024`
- `R025`
- `R029`
- `R032`
- `R033`
- `R035`
- `F001`
- `F003`
- `F004`
- `F006`
- `F014`
- `F015`

## Prerequisites

- `SB15,SB16`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowExecutorCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ApiEndpointRouteBuilderExtensions.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`

## Deliverables

- Consolidated test suite additions.
- API integration tests for plugin catalog/install/connection/health/executor catalog.
- Component tests for settings renderer and catalog.
- Playwright/browser proof for plugin catalog/settings/workflow editor/run.
- Regression tests for built-in executors and existing workflow catalog.

## Dependency Impact

- Final architecture review depends on this proof being complete.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run the targeted tests added throughout previous subbundles.
2. Add missing tests for duplicate ids, disabled plugins, invalid settings, secret authorization denial, renderer collisions, shop offline state, and OAuth2 fake flow.
3. Add API tests for plugin catalog/install/connection/health and workflow executor catalog.
4. Add component tests for plugin settings renderer/connection form.
5. Add Playwright tests or manual browser proof with screenshots for plugin catalog/settings/workflow editor/sample run.
6. Run full solution build if feasible.
7. Update execution report with all command and browser proof.

## Scope Exceptions

- Do not add new feature scope while closing proof.
- Do not skip proof because earlier subbundles passed.

## Do Not Do

- Do not mark final review ready without browser screenshots for UI surfaces.
- Do not ignore intermittent failures without documenting cause.
- Do not let old built-in workflow tests regress.

## Acceptance Checklist

- [ ] Unit, integration, component, and browser tests cover plugin MVP and future-facing seams.
- [ ] API and UI proof is captured and reviewed.
- [ ] Core workflow/built-in executor behavior is not regressed.
- [ ] Execution report contains enough evidence for final review.

## Proof Required

- `dotnet build CanDoItAll.slnx`
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "Plugin|WorkflowExecutor|Secret|SettingsSchema"`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "Plugin|WorkflowPlugin"`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "Plugin|SettingsRenderer"`
- `dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter "Plugin"`
- Browser screenshots recorded in reviews/01-execution-report.md.

## Browser Validation Logging

- Required. Catalog/settings/connection/workflow editor/sample workflow run. Use maximized desktop and narrower viewport for layout-sensitive pages.

## Progression Gate

- Passed only when test/API/browser proof covers MVP and no critical regressions remain.

## Suggested Agent Prompt

```text
Implement SB17 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
