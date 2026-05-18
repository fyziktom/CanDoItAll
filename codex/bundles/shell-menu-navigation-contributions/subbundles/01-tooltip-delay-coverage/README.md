# Tooltip Delay Coverage

## Status

- `Completed`

## Objective

- Make every remaining shell menu tooltip use the same few-second delay already used by opened-work floating menu cards.

## Success Criteria

- Standard sidebar navigation tooltips include a two-second delay.
- The bottom Settings menu tooltip includes a two-second delay.
- Popup trigger items `More`, `Opened`, and `Switch Database` remain without tooltips.
- Browser hover proof confirms no tooltip appears before the delay and a tooltip appears after the delay.

## Covered Inputs

- N001, R001, R002.

## Prerequisites

- Prepared-stage bundle validator passes.
- Existing popup trigger tooltip removals from the prior shell density work remain in place.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`

## Deliverables

- Shared delay applied to standard menu item TooltipTarget instances.
- Shared delay applied to the Settings utility TooltipTarget.

## Dependency Impact

- Final closure depends on tooltip behavior being calm and not interfering with popup panels. Weak proof here would reopen the browser validation phase.

## Validation Depth

- Critical UI foundation with browser-proof closure.

## Implementation Steps

1. Identify all remaining shell menu TooltipTarget usages.
2. Apply the shared delay to standard navigation and Settings tooltips.
3. Preserve the absence of trigger tooltips for popup menus.
4. Capture Playwright hover timing proof on desktop.

## Scope Exceptions

- None.

## Do Not Do

- Do not reintroduce tooltips on `More`, `Opened`, or `Switch Database` triggers.
- Do not redesign tooltip placement or floating panels.

## Acceptance Checklist

- Standard nav tooltip has a delayed TooltipTarget parameter.
- Settings tooltip has a delayed TooltipTarget parameter.
- Playwright check passes for before-delay and after-delay visibility.

## Proof Required

- Targeted component/build proof.
- Playwright MCP desktop route `/agents` at a large viewport.
- Screenshot `codex/bundles/shell-menu-navigation-contributions/evidence/menu-tooltip-delayed.png`.

## Browser Validation Logging

- Target route: `/agents`.
- Required viewport: desktop, `1440x900` or larger.
- Actions/assertions: hover a visible menu item; verify tooltip absent before 900ms and visible after at least 2 seconds.
- Screenshot: `evidence/menu-tooltip-delayed.png`.
- Review question: tooltip should not overlap or obstruct any popup trigger panel in this proof path.

## Progression Gate

- Playwright hover timing proof and code review confirm delayed tooltips without popup-trigger tooltip regressions.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
