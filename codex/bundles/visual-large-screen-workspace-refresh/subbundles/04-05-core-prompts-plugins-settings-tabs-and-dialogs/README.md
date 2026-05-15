# 04-core-prompts-plugins-settings-tabs-and-dialogs

## Status

- `Ready`

## Objective

- Redesign high-density core admin tab bodies and dialogs for plugins, prompt gallery, prompt factory, resources, and settings after shared BaseLib patterns are available.

## Covered Inputs

- RN-001 improve visual look, working space, and clarity.
- RN-007 use maximum desktop width.
- RN-008 design proposals for tab contents and dialogs.
- RN-009 use BaseLib/Tailwind/shared component mechanisms.
- RN-010 move excessive information into dialogs.
- RN-012 B2B video readiness.

## Prerequisites

- SB00-01 page inputs and proposals passed.
- SB00-03 reusable tree/detail/tab/dialog primitives passed.
- SB04 core workspace density pass has preserved route-level behavior or this subbundle owns the related route changes directly.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs\04-prompts-plugins-settings-resources.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages\05-core-pages-tabs-dialogs-proposal.png`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\Components\PromptFactorySupportLaneTabs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\Components\PromptFactoryDialogs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\Pages\ResourcesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\DatabaseSourcesSettingsPanel.razor`

## Deliverables

- Plugins tab bodies `Main info`, `Settings`, `Connections`, `Logs`, and `Grants` use dense detail and dialog/inspector surfaces.
- Prompt gallery detail bodies for identity, draft content, version history, and usage history use list/detail or dense tabs.
- Prompt Factory support lanes `Canvas`, `Setup`, `Governance`, `Assembly`, `Review` are visually consistent and full-width.
- Prompt Factory prompt preview, component editor, and impact confirmation dialogs use shared dialog primitives instead of adding custom modal styling.
- Resources and settings admin forms use compact list/detail and inspector dialog patterns where useful.
- Database transfer dialog remains reachable and visually aligned with the shell DB flow.

## Dependency Impact

- SB06 final proof depends on prompt/plugin/settings surfaces because these are likely video demo routes.
- If Prompt Factory cannot move away from page-local dialog styling without broad risk, record an exception and proof for the smallest safe improvement.

## Validation Depth

- High-risk UI and dialog proof, especially Prompt Factory.

## Implementation Steps

1. Start with plugins and settings because their tabs/dialogs are explicit and admin-heavy.
2. Move prompt gallery to tree/list/detail while preserving prompt CRUD/versioning.
3. Improve Prompt Factory support lane tab chrome and custom dialogs only within the smallest safe surface.
4. Use shared BaseLib dense tabs, dialog scaffold, toolbar, metric strip, and tree/detail primitives.
5. Preserve all service calls, route query behavior, and prompt/session/plugin state.
6. Add or update tests for moved tabs/dialogs.
7. Capture large-screen screenshots for each changed tab/dialog state.

## Scope Exceptions

- Do not fully rewrite `PromptFactoryPage.razor`; it is large and behavior-heavy.
- Do not remove existing prompt canvas functions or primitive previews.
- Do not tune mobile/medium.

## Do Not Do

- Do not add new custom modal/backdrop CSS.
- Do not hide OAuth/grant/security actions.
- Do not remove database transfer or settings management flows.

## Acceptance Checklist

- Plugins, prompt gallery, prompt factory, settings, and resources preserve all listed functions.
- Touched tabs and dialogs use shared component patterns.
- Prompt Factory custom dialog styling is reduced or exception documented.
- All changed tab/dialog states have large-screen screenshots.
- No new page-local CSS is added.

## Proof Required

- Targeted tests for moved plugin/prompt/settings interactions.
- Large-screen screenshots for changed core tab/dialog states.
- Open-state screenshots for Prompt Factory dialogs, plugin grant/connection/log states, and DB transfer.
- Diff review for no new page-local CSS.

## Browser Validation Logging

- Routes: `/plugins`, `/prompt-gallery`, `/prompt-factory`, `/resources`, `/settings`.
- Viewport: large desktop, recommended `1920x1080`.
- Actions: switch every changed tab, open prompt/plug-in/settings dialogs, edit/save/cancel representative forms, verify DB transfer and shell DB handoff.
- Screenshots: tab bodies and dialog open states.
- Review questions: do tabs feel like workspaces, are admin actions visible, are dialogs readable, and is the page professional enough for a recorded demo.

## Progression Gate

- Final visual proof cannot close core pages until changed tab/dialog states are proven or blockers are explicit.

## Suggested Agent Prompt

```text
Implement subbundle 04-05 only. Redesign core admin tab bodies and dialogs for plugins, prompt gallery, prompt factory, resources, and settings using shared dense tab/tree/detail/dialog patterns. Preserve all existing actions, avoid page-local CSS, run targeted tests, capture large-screen screenshots, and update the execution report.
```
