# Structured Input

## Core Objective

- Replace large header/tab stat-card rows with compact badge-style stats and icon-only tooltip-backed page-header actions, using a shared BaseLib implementation and proving the large-screen height savings with screenshots.

## Success Criteria

- Processes remains the visual reference and also uses the shared compact tooltip primitives.
- Targeted production pages no longer show large first-screen stat tiles in page headers or tab summary rows.
- Header add/open/refresh/similar actions changed by this bundle are icon-only with accessible labels and delayed tooltips.
- Badge stats have detail tooltips that wait 2 seconds before opening.
- Large-screen screenshots show reduced header/stat height without overlay or horizontal overflow.

## Hard Constraints

- Preserve existing data loading, commands, routes, and business behavior.
- Focus on large-screen layout only for this bundle.
- Use shared BaseLib primitives before page-local structural CSS.
- Use real browser proof for UI closure.

## Allowed Side Effects

- Shared BaseLib header/stat/action components and generated CSS may change.
- Production page/header/tab Razor markup may be recomposed to use the shared compact primitives.
- Bundle files and evidence artifacts may be updated.

## Source Artifacts

- Original request in `inputs/00-original-request.md`.
- Processes screenshot supplied in the user prompt.
- Processes implementation in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`.

## Input Coverage Signals

| ID | Raw note | Coverage |
| --- | --- | --- |
| N001 | Processes page is the correct example. | Shared primitive and `/processes` proof. |
| N002 | Other pages still have large stat cards. | Inventory and migration sweep. |
| N003 | Header add/refresh/similar buttons must be icon-only. | `PageHeaderActionButton` and page migrations. |
| N004 | Badges and icon actions need tooltip details. | Shared tooltip-backed primitives. |
| N005 | Tooltip delay must be 2 seconds. | Shared default and browser proof. |
| N006 | General prepared header component preferred. | `PageHeader` stats slot plus reusable compact stat/action components. |
| N007 | Tabs/subpages, especially CRM, must be improved. | CRM-HR routes and selected tab/subpage stat rows. |
| N008 | Validate with screenshots; save height; avoid overflow. | Large-screen browser proof and screenshot review. |

## Dependency And Sequencing Signals

- Shared primitive work must land before page migrations so every route inherits the same tooltip and density policy.
- Browser proof depends on completed shared CSS and migrated route markup.

## Validation Expectations

- `dotnet build` for the solution or affected projects.
- Large-screen browser screenshots for `/processes`, CRM-HR routes, and representative non-CRM pages.
- Tooltip open-state proof for a compact stat and a header action after the 2-second delay.

## Evidence Contract

- Command output recorded in `reviews/01-execution-report.md`.
- Screenshot paths recorded in `reviews/01-execution-report.md`.
- Raw-note closure table updated for N001-N008.

## UI Validation Strategy

- Use a large viewport around the provided screenshot proportions, starting at 1600x900 or wider.
- Review: header height, stat row height, row wrapping, tooltip readability, clipping, lateral overflow, and visual alignment.
- Narrower-width tuning is intentionally deferred unless a large-screen validation failure points to shared CSS breakage.

## Browser Validation Analytics

- Log route, viewport, Playwright/browser actions, screenshot path, and pass/fail in the execution report for each proof route.

## Working Assumptions

- Badge copy should stay short while tooltip text carries the detailed helper explanation.
- Header descriptions can be suppressed on compact migrated headers to save vertical height.

## Primary Risks

- Rows with many stats may wrap at 1600px if actions and title are too verbose.
- Tooltip timing is easy to under-test; checks must wait long enough.
- Generated BaseLib CSS must stay in sync with Tailwind source changes.
