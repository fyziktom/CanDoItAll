# SB04 Plugin Icon Assets And Rendering Contract

## Status

- `Completed`

## Objective

Add a typed plugin icon contract and local Docker/Gmail/Office365 icon assets so plugins render consistently in the plugins page, workflow canvas menu, and workflow executor nodes.

## Success Criteria

- Plugin icons are modeled with a typed descriptor or equivalent strongly typed contract.
- Docker, Gmail, and Office365 have reviewed local icon assets or explicit Material icon fallbacks.
- Plugin page, workflow menu, and executor node rendering use the same icon source where practical.
- Installed package icon paths are resolved safely and cannot traverse package roots.
- Browser proof captures all three target surfaces.

## Covered Inputs

- PRH-008 Plugin Icon Contract
- PRH-007 Workflow Canvas Plugin Executor Menu, icon-related rendering only
- FIND-007

## Prerequisites

- SB01 progression gate passed.
- SB03 completed if menu icon proof depends on the nested plugin menu.
- Read `inventories/03-icon-asset-plan.md`.
- Read the `Icon Assets` rows in `inventories/plugin-runtime-architecture-hardening-checklist.xlsx`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowExecutorCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginPackageServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginManifestContracts.cs`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Docker`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail`
- `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365`

## Deliverables

- Typed icon descriptor model with explicit icon kinds.
- Safe package icon asset resolver.
- Local asset or fallback assignment for Docker, Gmail, and Office365.
- Plugin catalog/page rendering updated to use plugin icon descriptors.
- Workflow canvas menu and executor node rendering updated to use plugin icon descriptors where applicable.
- Tests for asset path safety and fallback behavior.

## Dependency Impact

- SB06 uses this work to package Docker with its icon and prove Docker executor nodes/menu entries render correctly after manual install.

## Validation Depth

- `UI, asset, and browser-proof`

## Implementation Steps

1. Decide the minimal typed icon model consistent with existing descriptor patterns.
2. Add plugin icon metadata to catalog descriptors/items without breaking existing package manifest compatibility.
3. Add or select local icon assets for Docker, Gmail, and Office365 after checking the source guidance.
4. Add explicit Material icon fallbacks if brand assets are not approved.
5. Add safe package icon resolution for installed package assets.
6. Update plugin page rendering.
7. Update workflow canvas menu rendering to show plugin group icons.
8. Update executor node rendering to show plugin icon as the small node plugin mark, while preserving executor-specific icon semantics if both are useful.
9. Add tests for icon descriptor serialization/resolution/path traversal/fallbacks.
10. Capture browser proof and update the execution report.

## Scope Exceptions

- Do not do legal/trademark approval in code. Record the chosen source and fallback.
- Do not hotlink external SVG/PNG URLs.
- Do not redesign all executor visual styling unless required to fit the small plugin icon.

## Do Not Do

- Do not introduce raw plugin id to icon path switch statements in UI components.
- Do not expose arbitrary package file paths as web assets.
- Do not hide missing icon resolution failures without explicit fallback behavior.

## Acceptance Checklist

- [x] Plugin icon model is strongly typed.
- [x] Docker/Gmail/Office365 have icon assignments.
- [x] Package icon paths are validated against traversal.
- [x] Plugins page renders plugin icons.
- [x] Workflow context menu renders generic/plugin icons.
- [x] Executor nodes render a small plugin icon.
- [x] Tests and browser proof are captured.

## Proof Required

- Unit tests for icon resolution and unsafe paths.
- Component/browser proof for plugins page icons.
- Browser proof for workflow context menu icons and executor node plugin mark.
- Execution report with chosen asset sources or fallback note.

## Browser Validation Logging

- Target routes: `/plugins` and workflow canvas/editor route.
- Required viewport passes: maximized desktop and one narrower width if icon/text alignment changes.
- Required actions: inspect plugin list/cards, open workflow context menu to plugin layer, create plugin executor node, inspect node icon.
- Screenshot evidence: `artifacts/sb04-plugin-page-icons.png`, `artifacts/sb04-canvas-plugin-menu-icons.png`, `artifacts/sb04-canvas-node-plugin-icon.png`.
- Review questions: Are icons recognizable and local? Does text still fit? Does missing/fallback icon behavior look intentional?

## Progression Gate

- SB06 may package Docker only after Docker icon metadata is available either as a reviewed local asset or a documented fallback included in the package manifest.

## Suggested Agent Prompt

```text
Implement SB04 only from C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-architecture-hardening-followup.
Add a strongly typed plugin icon contract, safe asset resolution, and Docker/Gmail/Office365 icons or documented fallbacks. Verify plugins page, workflow menu, and executor node rendering with tests and browser screenshots. Update reviews/01-execution-report.md.
```
