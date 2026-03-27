
# Screenshot validation strategy

Screenshot validation is a blocking quality gate for every UI-changing item.

## Artifact convention

Store screenshot artifacts under a predictable path, for example:

`artifacts/screenshots/<item-id>/<scenario-name>.png`

Recommended scenario-name format:

- `catalog-open`
- `canvas-after-create`
- `details-panel`
- `context-menu`
- `modal-open`
- `popover-right`
- `popover-left-fallback`
- `toolbox-scrolled`
- `placement-before`
- `placement-after`
- `export-output`

## Minimum evidence rules

- At least two screenshots for simple UI items.
- At least three screenshots for floating toolbox items.
- Before/after evidence for spatial bugfixes such as side-aware placement.
- One screenshot must prove the exact interaction result, not only the entry point.

## Semantic review template

For every screenshot set, write a short note answering:

1. What UI state is shown?
2. Which acceptance criterion does it prove?
3. What is visibly correct?
4. What would still fail the item even if the screenshot exists?

## Playwright-first recommendation

Where practical, add Playwright scenarios that:
- open the relevant canvas,
- perform the interaction,
- capture screenshots,
- save them as named artifacts.

Manual screenshots are still allowed when the scenario is hard to automate, but they must be explained.

## Blocking rule

Missing, irrelevant, or low-value screenshots mean the item is **not done**.
