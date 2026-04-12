# Fullscreen Template Dialog And List Shell

## Status

- `Completed`

## Objective

- Replace the old baseline action with a fullscreen templates dialog and build the searchable tabbed list shell plus overlay-safe notification behavior.

## Covered Inputs

- Replace `Seed development baseline` with `Templates`.
- Use a fullscreen modal.
- Keep a searchable, scrollable left list panel with category tabs.
- Keep notifications visible above the modal.

## Prerequisites

- `subbundles/01-library-foundation-and-preview-models`

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals\Dialog.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\Notification.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Lists\ListDetailShell.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Tabs.razor

## Deliverables

- New templates dialog component integrated into `ProcessWorkspace`.
- Header and empty-state action rename from baseline seeding to templates browsing.
- Search box and category tabs inside the modal list pane.
- Selection cards for process, role, and artifact categories.
- Notification z-index or overlay stacking fix that keeps toast feedback visible over the dialog.

## Dependency Impact

- The preview and import phase depends on this shell because the later proof assumes stable modal state, selection behavior, and category filtering.
- Weak proof here would make later render and import bugs indistinguishable from shell state bugs.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Add modal open and close state to `ProcessWorkspace`.
2. Replace the old baseline buttons with `Templates`.
3. Create the fullscreen dialog component and wire it to BaseLib `Dialog`.
4. Build the left list shell with search, category tabs, scrollable cards, and selection state.
5. Raise success notifications through the shared notification service and adjust notification stacking above the modal.

## Scope Exceptions

- The rich preview tabs and import mutations close in the next phase.

## Do Not Do

- Do not route the user away from the Process management page.
- Do not replace the shared BaseLib dialog with custom page overlay markup.
- Do not close the modal automatically after imports.

## Acceptance Checklist

- The workspace exposes `Templates` instead of the old baseline action.
- Opening the dialog shows a fullscreen modal.
- The left pane filters and switches categories correctly.
- Selecting an item updates modal state cleanly.
- Notifications are visually above the modal overlay.

## Proof Required

- Updated component tests for the new workspace entry and modal shell.
- Browser proof that opens the modal, switches categories, searches, and shows a toast over the open modal.
- Screenshot evidence for the fullscreen modal and toast stacking.

## Browser Validation Logging

- Route under test: `/processes`
- Required viewports: desktop `1900x1200`, then narrower-width follow-up only if the shell layout changes materially.
- Required Playwright actions: open the modal, switch all category tabs, exercise search, trigger a toast-producing action, verify the modal remains open.
- Required screenshots: fullscreen templates dialog, filtered category list, toast over modal.
- Required screenshot review questions: does the left pane remain readable and scrollable, and is the notification clearly above the modal overlay.

## Progression Gate

- Preview and import work may continue only after the modal shell is stable and the toast overlay is visibly above the modal.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Build the fullscreen templates dialog shell, replace the old baseline action, and make notification stacking safe above the modal.
```
