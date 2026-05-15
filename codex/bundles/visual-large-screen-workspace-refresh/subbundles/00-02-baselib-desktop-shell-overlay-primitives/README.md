# 00-baselib-desktop-shell-overlay-primitives

## Status

- `Ready`

## Objective

- Build or extend reusable BaseLib/shared shell primitives for the collapsed desktop command rail, right-side tooltips, bottom utility actions, and safe hover flyouts before the app shell is refactored.

## Covered Inputs

- RN-003 use the Economy Simulator collapsed menu concept.
- RN-004 reduce menu text and move information to tooltips.
- RN-005 move Settings and Switch DB to bottom-left menu.
- RN-006 show DB info flyout with safe copy.
- RN-007 use maximum available page width.
- RN-009 no own CSS; prefer BaseLib/Tailwind/component options.
- RN-011 shared component preference.

## Prerequisites

- SB00-01 page-function and proposal gate passed.
- Current `AppShell`, `TooltipTarget`, `CopyButton`, and database layout flows are understood.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inventories\02-reusable-baselib-component-candidates.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages\01-shell-baselib-corrected-proposal.png`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages\07-baselib-reusable-components-proposal.png`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\TooltipTarget.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Buttons\CopyButton.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayoutTopBar.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayoutDatabaseDialog.razor`

## Deliverables

- Reusable collapsed/expanded desktop rail primitive or `AppShell`-owned composition with typed item model.
- Standard tooltip/flyout pattern for right-side rail help and bottom DB flyout.
- Bottom utility action slots for Settings, Switch DB, Help, and future shell utilities.
- Safe copy pattern for non-secret DB summary text.
- Component examples/sandbox coverage or focused tests proving collapsed, expanded, tooltip, and flyout states.

## Dependency Impact

- SB02 shared shell implementation depends on these primitives.
- Every page-level screenshot depends on shell width and overlay correctness.

## Validation Depth

- Critical UI foundation with component and browser proof.

## Implementation Steps

1. Decide whether to add a new BaseLib shell primitive or extend `AppShell` with strongly typed rail sections. Prefer the smallest maintainable change.
2. Use `TooltipTarget` for right-side tooltip behavior unless a reusable richer flyout primitive is needed.
3. Add bottom utility slots/actions with stable test ids and keyboard/focus behavior.
4. Add a safe-copy content contract that can mask DB/server/user values and avoid raw connection strings.
5. Add component examples or focused tests for collapsed rail, expanded rail, tooltip, flyout, and copy action.
6. Do not wire product DB behavior until the primitive proof passes.

## Scope Exceptions

- Do not redesign all navigation routes in this foundation subbundle.
- Do not replace `MainLayoutDatabaseDialog`; this only provides the shell/flyout path to it.
- Do not tune mobile navigation.

## Do Not Do

- Do not copy Economy CSS.
- Do not add page-local CSS.
- Do not expose raw database connection strings, passwords, tokens, or provider credentials.
- Do not introduce a second icon library.

## Acceptance Checklist

- Rail primitive supports collapsed default and expanded state.
- Tooltips open to the right and are not clipped at desktop viewport.
- Bottom utility area exists and supports Settings/DB actions.
- Safe DB flyout content can display active DB facts and copy a safe summary.
- Tests or examples prove overlay and copy behavior.

## Proof Required

- Component/unit tests for rail/flyout state or equivalent bUnit coverage.
- Large-screen browser screenshots for collapsed rail, expanded rail, tooltip, and DB flyout after SB02 wiring.
- Diff review showing no new page-local CSS and no unsafe DB copy string.

## Browser Validation Logging

- Routes: component sandbox if available, then `/`, `/projects`, `/settings` after shell wiring.
- Viewport: large desktop, recommended `1920x1080`.
- Actions: open collapsed rail, hover/focus nav item, expand rail, hover/focus DB utility, click copy, open DB dialog.
- Screenshots: collapsed, expanded, tooltip, DB flyout, DB dialog handoff.
- Review questions: is the rail compact, is tooltip readable, is flyout safe and unclipped, and is the topbar free of DB state.

## Progression Gate

- SB02 may start only after the reusable rail/flyout primitive contract is clear and tested enough to avoid one-off shell markup.

## Suggested Agent Prompt

```text
Implement subbundle 00-02 only. Build or extend shared shell/overlay primitives for a collapsed desktop command rail, right-side tooltip, bottom utility actions, and safe DB hover flyout. Use BaseLib/Tailwind/shared component mechanisms, add focused tests/examples, and stop before page-specific redesign.
```
