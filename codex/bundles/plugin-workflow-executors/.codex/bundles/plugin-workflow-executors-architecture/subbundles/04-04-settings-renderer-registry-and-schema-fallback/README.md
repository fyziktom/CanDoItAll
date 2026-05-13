# Settings Renderer Registry And Schema Fallback

## Status

- `Completed`

## Objective

- Create renderer registry and schema fallback UI; begin de-hardcoding workflow settings UI.

## Success Criteria

- A renderer registry exists for trusted/bundled settings components.
- A schema fallback settings renderer exists and supports the canonical field types.
- Renderer keys are collision-safe and duplicate registrations fail predictably.
- Workflow executor settings UI has a path away from hard-coded per-executor branches.

## Covered Inputs

- `R005`
- `R014`
- `R017`
- `R023`
- `R028`
- `R029`
- `F004`
- `F005`

## Prerequisites

- `SB03`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowExecutorCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Connectors\ConnectorManifest.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Connectors\ConnectorConfigState.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\ConnectorConfigFieldEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`

## Deliverables

- ISettingsRendererRegistry or plugin-ready equivalent.
- Schema fallback Blazor component.
- Renderer host component using DynamicComponent for trusted/bundled renderers.
- Duplicate renderer key tests.
- At least one workflow/settings UI surface using the renderer host.

## Dependency Impact

- Later plugin module, workflow bridge, settings UI, and shop work depend on this foundation. Weak proof here causes duplication, secret leakage, or unstable plugin boundaries later.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Design renderer descriptor fields: renderer key, component type, trust level, supported schema version, owner id.
2. Create renderer registry with duplicate-key startup failure or deterministic conflict handling.
3. Create schema fallback component using canonical configuration field editor behavior.
4. Create renderer host component that selects custom trusted renderer or fallback.
5. Refactor the workflow executor settings editor enough to prove new plugin executors can avoid hard-coded UI branches.
6. Add component tests for fallback fields and renderer registry selection.
7. Add browser proof if the workflow/settings UI changed.

## Scope Exceptions

- Do not fully rewrite the workflow canvas if a smaller host integration proves the seam.
- Remote/untrusted renderer execution remains out of scope.

## Do Not Do

- Do not add `if pluginId == ...` branches.
- Do not trust renderer metadata from remote shop packages as executable UI.
- Do not duplicate ConnectorConfigFieldEditor logic in page-local markup.

## Acceptance Checklist

- [x] A renderer registry exists for trusted/bundled settings components.
- [x] A schema fallback settings renderer exists and supports the canonical field types.
- [x] Renderer keys are collision-safe and duplicate registrations fail predictably.
- [x] Workflow executor settings UI has a path away from hard-coded per-executor branches.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "SettingsRenderer|ConfigurationField"`
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SettingsRenderer"`
- `dotnet build src\CanDoItAll.Modules.AgentFramework\CanDoItAll.Modules.AgentFramework.csproj`

## Browser Validation Logging

- Completed on `2026-05-13 12:58:50 -04:00`.
- Route: `http://localhost:5032/agents/workflows`.
- Verified the workflow editor renders a schema-backed fallback settings form for a `Workspace files` executor node.
- Desktop screenshot: `C:\repositories\CanDoItAll\codex\bundles\plugin-workflow-executors\.codex\bundles\plugin-workflow-executors-architecture\artifacts\sb04-settings-renderer\sb04-workflow-settings-desktop.png`.
- Narrow screenshot: `C:\repositories\CanDoItAll\codex\bundles\plugin-workflow-executors\.codex\bundles\plugin-workflow-executors-architecture\artifacts\sb04-settings-renderer\sb04-workflow-settings-narrow.png`.

## Progression Gate

- Passed. Workflow executor settings now route through `SettingsRendererHost` and `ConfigurationSchemaFallbackRenderer`, so plugin settings can render from canonical schema metadata without adding per-plugin hard-coded branches.

## Suggested Agent Prompt

```text
Implement SB04 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
