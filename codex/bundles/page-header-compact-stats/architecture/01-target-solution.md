# Target Solution

## Shared UI Shape

- Extend `PageHeader` with a `Stats` slot so page title, badge stats, and actions can live in one compact header row on large screens.
- Add a `CompactStatStrip` + `CompactStat` pair in BaseLib for reusable badge-style stats outside headers and inside tabs/subpages.
- Add a `PageHeaderActionButton` wrapper in BaseLib for icon-only header actions with shared tooltip defaults and accessible labels.
- Use `TooltipTarget Delay=TimeSpan.FromSeconds(2)` inside the new primitives so callers do not repeat timing policy.

## Migration Boundary

- Convert the identified production page headers and high-value tab/subpage summary rows.
- Keep deeper modal-only metric rows out of the critical path unless they block screenshots or are part of a visible page/tab summary.
- Preserve existing services, route logic, data loading, and list/detail workflows.

## Styling Boundary

- Add compact stat/header styles to shared Tailwind files and rebuild BaseLib CSS.
- Do not add page-local structural CSS for every migrated page unless the shared primitive cannot express the layout.
