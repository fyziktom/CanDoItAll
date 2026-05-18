# 01-shared-compact-header-primitives

## Status

- `Completed`

## Objective

- Add shared BaseLib compact stat and icon-only header action primitives, and extend `PageHeader` so page stats can live in the shared header rather than separate large tile rows.

## Covered Inputs

- N001, N003, N004, N005, N006

## Prerequisites

- Bundle prepared-stage validator passed or repaired.
- Current processes implementation inspected as the visual reference.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\PageHeader.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Badges\StatusBadge.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\TooltipTarget.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Buttons\Button.razor`
- `C:\repositories\CanDoItAll\Tailwind\navigation\page-header.css`
- `C:\repositories\CanDoItAll\Tailwind\layout\stats.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`

## Deliverables

- `CompactStatStrip` and `CompactStat` shared components.
- `PageHeaderActionButton` shared component.
- `PageHeader` supports a compact stats slot.
- Shared CSS supports compact one-row page headers and compact stat badge rows.
- Processes page top command strip uses the shared compact stat/action tooltip components.

## Dependency Impact

- This is a critical UI foundation. Every migrated page depends on its tooltip timing, accessibility labels, wrapping behavior, and compact spacing.

## Validation Depth

- Critical UI foundation with build proof and at least one browser proof on `/processes` before downstream migration closes.

## Implementation Steps

1. Extend `PageHeader` with a `Stats` render fragment and compact row styling.
2. Add `CompactStatStrip` and `CompactStat` with default `TimeSpan.FromSeconds(2)` tooltip delay.
3. Add `PageHeaderActionButton` with icon-only button rendering, accessible label/title, and default delayed tooltip.
4. Update shared Tailwind CSS and generated BaseLib output CSS.
5. Convert the processes command strip badges/actions to the shared primitives without changing behavior.

## Scope Exceptions

- This phase does not migrate every page; it only creates the shared foundation and updates the reference processes header.

## Do Not Do

- Do not change process data loading, commands, or runtime behavior.
- Do not add per-page tooltip timing constants.
- Do not tune medium/mobile layout unless required to avoid a build or large-screen blocker.

## Acceptance Checklist

- Shared components compile.
- Processes header remains compact.
- Compact stat and header action tooltips default to 2 seconds.
- Header actions are icon-only with labels.

## Proof Required

- `dotnet build` for affected projects or solution.
- Large-screen `/processes` screenshot after conversion.
- Tooltip open-state proof for one process stat and one process header action after the 2-second delay.

## Browser Validation Logging

- Route: `/processes`.
- Viewport: at least 1600x900 or maximized equivalent.
- Actions: navigate, capture header screenshot, hover a compact stat, wait for 2 seconds, capture tooltip, hover an action, wait for 2 seconds, capture tooltip.
- Screenshot review questions: row height saved, no horizontal overflow, no tooltip clipping, icon actions remain clear through tooltip/aria label.

## Progression Gate

- Downstream migration may start only after the shared primitives compile and `/processes` still renders a compact header with delayed tooltip behavior.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Add shared BaseLib compact stat/action primitives, extend PageHeader with a stats slot, convert the processes command strip to those primitives, rebuild shared CSS, run build proof, and record /processes browser evidence. Stop if tooltip delay or compact row wrapping cannot be proven.
```
