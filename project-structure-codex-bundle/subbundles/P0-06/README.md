# P0-06 Runtime Surface Cleanup And Support-Demo Separation

## Status
- Lifecycle status: `Ready`

## Objective
- Slim the runtime page and separate production authoring UI from support and demo surfaces.

## Covered Inputs
- Audit recommendation to remove always-on demo clutter from the runtime page.
- Feature preservation items `F01`, `F06`, `F26`, and `F27`.

## Prerequisites
- None.

## Exact Source References
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`

## Deliverables
- Runtime page focused on authoring surfaces rather than permanent demo cards.
- Clear handling for outline and graph-health support surfaces.
- User-facing structure page with lighter chrome and preserved access to required workflows.

## Dependency Impact
- Unlocks `P1-04` because overlay and support-surface decomposition is cleaner after runtime cleanup.
- May affect screenshots and default layout proof for later browser tasks.

## Validation Depth
- Targeted ProjectStructure tests around runtime chrome.
- Browser proof for the cleaned runtime shell.
- Screenshot review to confirm the page still uses space intentionally.

## Implementation Steps
- Inspect which support or demo cards still render by default.
- Remove, gate, or relocate them without dropping required user workflows.
- Update tests or screenshots for the intended runtime layout.

## Do Not Do
- Do not remove a support surface without a deliberate replacement or documented exception.
- Do not widen into selection-panel decomposition owned by `P1-04`.

## Acceptance Checklist
- ProjectStructure runtime page no longer renders always-on demo cards.
- User-facing runtime behavior is clearer and lighter.
- Any moved support functionality remains reachable where intended.

## Proof Required
- Targeted ProjectStructure tests.
- Playwright route proof for the cleaned runtime shell.
- Large-screen screenshot review.

## Browser Validation Logging
- Route: ProjectStructure structure route.
- Viewport: large-screen first, narrower width if layout changes materially.
- Log screenshots and review notes in `reviews/01-execution-report.md`.

## Progression Gate
- Do not start `P1-04` until the runtime shell is intentionally slimmed and the relocated support surfaces are still reachable.

## Suggested Agent Prompt
- Audit the runtime page for always-on demo and support surfaces, then make the smallest layout cleanup that clarifies authoring behavior without dropping needed functionality.
