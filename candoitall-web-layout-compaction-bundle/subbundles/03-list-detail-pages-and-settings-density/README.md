# list-detail-pages-and-settings-density

## Status

- `Completed`

## Objective

- Apply the compact large-screen density rules to the dashboard and repeated operational routes so headers, summaries, tabs, list headers, and filter bars stop consuming more height than the actual work surfaces.

## Covered Inputs

- Request note to analyze other pages and think how to make the UI more compact
- Request note about using helper-copy tooltips where they save meaningful space
- Request note about component flexibility and shared patterns

## Prerequisites

- `subbundles/01-shell-foundations-and-layout-primitives`
- `subbundles/02-projects-page-and-project-modals` should be trusted as the reference implementation

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Pages\Home.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\Pages\ResourcesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity\Pages\ActivityPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Pages\AutomationPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Validation\Pages\ValidationCenterPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.TestLab\Pages\TestLabPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\DatabaseSourcesSettingsPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\ProjectStructureAgentSettingsPanel.razor`

## Deliverables

- Compact header and summary usage on dashboard and operational routes.
- Repeated list/detail pages using the shared dense filter-row pattern.
- Settings route using less pre-content height before the active tab surface.
- Helper text moved behind compact affordances where it saves meaningful vertical budget.

## Dependency Impact

- This subbundle spreads the projects reference pattern across the routes most users will revisit.
- If this phase is skipped or weak, the app will still feel inconsistent even if `/projects` improves.

## Validation Depth

- `UI, component-test, and browser-proof`

## Implementation Steps

1. Compact the dashboard header and early sections so work entry points dominate the first screen.
2. Apply the shared dense filter-row pattern to resources, prompt gallery, validation, test lab, activity, automation, and settings list headers.
3. Re-evaluate summary tiles and tab placement on settings and prompt gallery so the route reaches actual content sooner.
4. Use help affordances to relocate secondary helper copy where it materially improves density.

## Scope Exceptions

- Do not remove any route’s core summary counts if they still carry operational value; compact them instead.

## Do Not Do

- Do not make every route visually identical if the working surface type is different.
- Do not push important destructive or primary actions into hidden affordances.
- Do not collapse detail-pane editor context so far that orientation is lost.

## Acceptance Checklist

- Dashboard, settings, and at least three list/detail routes reach the working surface faster on desktop.
- Repeated filter bars align controls more efficiently on wide screens.
- Settings tabs and detail panes remain readable after compaction.
- Helper-copy affordances remain discoverable and usable.

## Proof Required

- Browser proof on:
  - `/dashboard`
  - `/resources`
  - `/prompt-gallery`
  - `/settings`
  - one of `/validation` or `/test-lab`
- Large-screen screenshots and one narrower-width follow-up for at least two routes

## Browser Validation Logging

- Target routes: `/dashboard`, `/resources`, `/prompt-gallery`, `/settings`, `/validation`, `/test-lab`, `/activity`, `/automation` as touched
- Viewports: `1720x1160`, `1280x900`
- Required browser actions:
  - open route
  - inspect header, summary, tabs, list header, and filter area
  - open any help affordance introduced for compressed copy
- Required screenshot paths:
  - `output/playwright/subbundle-03-dashboard-large.png`
  - `output/playwright/subbundle-03-resources-large.png`
  - `output/playwright/subbundle-03-prompt-gallery-large.png`
  - `output/playwright/subbundle-03-settings-large.png`
- Required review answers:
  - does each route reach the main task faster?
  - did compaction remove redundancy instead of information?

## Progression Gate

- Subbundle 04 may proceed only after at least the dashboard, one list/detail route, and settings have reviewed desktop proof showing the density pattern works outside `/projects`.

## Suggested Agent Prompt

```text
Implement only subbundle 03.
Reuse the shared density patterns from subbundles 01 and 02.
Do not treat the route list as a copy-paste exercise; compact each page according to its working surface while keeping the rules coherent.
```
