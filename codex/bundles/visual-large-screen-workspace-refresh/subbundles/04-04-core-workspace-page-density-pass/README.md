# 04-core-workspace-page-density-pass

## Status

- `Completed`

## Objective

- Apply the new shell and tree patterns to core workspace pages so the main app looks professional and uses large-screen width well: dashboard, agents, resources, plugins, prompt gallery, prompt factory, and settings.

## Covered Inputs

- RN-001 improve visual look, working space, and clarity.
- RN-007 use maximum available width.
- RN-008 analyze page screenshots and repair until visually aligned.
- RN-009 no own CSS; use Tailwind/BaseLib/component options.
- RN-010 use dialogs when pages contain too much information.
- RN-012 professional B2B customer-video readiness.

## Prerequisites

- SB00-03 reusable layout/tab/dialog primitives passed.
- SB02 shell foundation passed.
- SB03 tree patterns passed for hierarchical surfaces.
- SB01 route baseline and proposals exist for each owned route.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Pages\Home.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\Pages\ResourcesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\DatabaseSourcesSettingsPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Layout\PageScaffold.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\PromptFactoryBrowserTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\PromptLibraryVerificationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\DatabaseSwitchWorkbenchPlaywrightTests.cs`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\04-prompts-plugins-settings-resources.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages\05-core-pages-tabs-dialogs-proposal.png`

## Deliverables

- Dashboard compacted into a clearer operational entry surface.
- Agents page density pass with provider/agent details moved to compact panels, tabs, or dialogs where appropriate.
- Resources/plugins/prompt gallery pages use full-width workspaces with concise list/tree grouping and dialog details.
- Prompt factory preserves focused workbench behavior while removing wasted space and moving secondary metadata into dialogs/floating inspectors.
- Settings remains the deep management page and integrates cleanly with shell DB/Settings entry points.
- Deep tab/dialog work for plugins, prompt gallery, prompt factory, resources, and settings is coordinated with subbundle `04-05-core-prompts-plugins-settings-tabs-and-dialogs`.
- Route inventory updated with before/after visual assessment for every owned route.

## Dependency Impact

- SB06 depends on these pages for the primary customer video path and final visual quality.
- Settings and prompt/provider pages depend on SB02 DB shell behavior; broken handoff requires reopening SB02.

## Validation Depth

- UI, component-test, and browser-proof.

## Implementation Steps

1. For each owned route, open the baseline screenshot and route proposal from SB01.
2. Make the smallest page-level changes needed to use more width and reduce explanatory chrome.
3. Prefer `PageScaffold MaxWidthClass="max-w-none"`, `FillHeight`, `Grid`, `Stack`, `Tabs`, `SecondaryTabs`, `Dialog`, `DialogScaffold`, and shared component `Class` parameters.
4. Move low-frequency details into dialogs/flyouts and keep primary actions visible.
5. Preserve existing route state, query-string behavior, and service calls.
6. Add or update tests for any moved interaction.
7. Capture large-screen after screenshots and update the route inventory.

## Scope Exceptions

- Do not attempt a full product redesign of every component inside `PromptFactoryPage.razor`; focus on large-screen workspace clarity and high-visible chrome.
- Do not duplicate tab/dialog-specific work owned by subbundle `04-05-core-prompts-plugins-settings-tabs-and-dialogs`; either complete it there or record the handoff.
- Do not migrate unrelated existing page-local CSS unless it is directly touched by the refresh.

## Do Not Do

- Do not create marketing hero sections.
- Do not add new custom CSS or `.razor.css` files.
- Do not remove access to settings/provider/database management actions.
- Do not collapse important operational status so far that the page becomes ambiguous.

## Acceptance Checklist

- Each owned route has a large-screen after screenshot.
- Each owned route uses visibly more workspace or has an explicit low-change rationale.
- Explanatory copy is reduced or moved to tooltips/dialogs where it was crowding the page.
- No new page-local custom CSS was added.
- Existing critical actions remain reachable and tested.

## Proof Required

- Relevant unit/component tests for moved interactions.
- Playwright proof for dashboard, agents, resources/plugins or equivalent, prompt gallery/factory, and settings.
- Large-screen screenshots for every owned route.
- Open-state screenshots for new or changed dialogs/flyouts.
- Diff review for no new page-local CSS.

## Browser Validation Logging

- Routes: `/`, `/agents`, `/resources`, `/plugins`, `/prompt-gallery`, `/prompt-factory`, `/settings`.
- Viewport: large desktop, recommended `1920x1080`.
- Actions: navigate, exercise primary tabs/filters, open moved detail dialogs, verify settings and DB pathways.
- Screenshots: one full-page after screenshot per route plus open-state screenshots for changed dialogs.
- Review questions: did width usage improve, is the primary workflow clearer, did copy shrink, are details still reachable, and does the result match the B2B reference direction.

## Progression Gate

- SB06 may close core pages only after every owned route has an after screenshot, no new CSS violation, and all moved interactions have proof or explicit blocker rows.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Apply the large-screen shell/tree design direction to dashboard, agents, resources, plugins, prompt gallery, prompt factory, and settings. Keep changes small and route-specific, use shared components/Tailwind only, move dense details into dialogs where useful, run targeted tests, capture large-screen screenshots, and update the execution report.
```
