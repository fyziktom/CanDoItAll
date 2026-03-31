# Structured Input

## Core Objective

- Replace CanDoItAll icon delivery with locally hosted Google Material Icons instead of the current remote Font Awesome path.
- Put the shared icon foundation inside the solution, centered on `CanDoItAll.Components.BaseLib`.
- Create a workbook-driven migration inventory before broad edits so implementation can be tracked file by file and token by token.

## Hard Constraints

- Use the `candoitall-bundle-workflow` process instead of jumping straight into implementation.
- Do not keep icon delivery connected to a CDN or any other runtime external resource.
- Cover both `Icon.razor` call sites and pure icon surfaces rendered via spans, glyph text, inline button text, token previews, or raw shorthand badges.
- Respect the current dirty worktree and merge carefully around existing user edits in the Workbench and BaseLib files already modified locally.
- Treat generated assets such as `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css` as derived outputs, not as the primary manual edit target.

## Source Artifacts

- `C:/repositories/CanDoItAll/material-icons-migration-bundle-v1/inputs/00-original-request.md`
- `https://fonts.googleapis.com/icon?family=Material+Icons`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Web/Components/App.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Identity/Icon.razor`
- `C:/repositories/CanDoItAll/output/spreadsheet/material-icons-migration-tracker.xlsx`
- `C:/repositories/CanDoItAll/material-icons-migration-bundle-v1/inventories/01-material-icon-inventory.csv`
- `C:/repositories/CanDoItAll/material-icons-migration-bundle-v1/inventories/02-material-icon-token-map.csv`

## Input Coverage Signals

- The request explicitly says `map all places` and `pure icons are used`, so the census cannot stop at shared components or existing `<Icon>` usage alone.
- The user explicitly asked for `some excel` to track done versus remaining work, so the workbook is a required deliverable, not a convenience.
- The user explicitly named `Icon.razor` in BaseLib but also said the change should live as part of the solution, so the migration scope must cover the full runtime icon path, including the web head asset load.
- The user explicitly said `Do not connect it just as external resource`, so any runtime stylesheet or font URL left remote is a failure against the raw note.

## Dependency And Sequencing Signals

- Inventory and workbook generation must land before wide edits so progress and remaining scope stay auditable.
- Local asset delivery and shared renderer conversion must land before broad call-site cleanup; otherwise later route proof would be tied to unstable foundations.
- Shared component migration must happen before page and module cleanup because shell, tabs, buttons, and tree rows are reused broadly.
- Workbench and canvas surfaces should come after the shared icon foundation because they have the heaviest token mapping and the highest collision risk with current local edits.

## Validation Expectations

- Pass `codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py --stage prepared` before product code changes.
- Remove the remote icon stylesheet from `C:/repositories/CanDoItAll/src/CanDoItAll.Web/Components/App.razor` and replace it with local static assets.
- Keep the workbook and CSV exports current through execution so every moved row or resolved token has visible status.
- Prove the final migration with at least a clean solution build, targeted tests where available, and browser proof on shared-shell, module, and Workbench routes.

## UI Validation Strategy

- Start with a maximized or large-screen desktop pass on `/`, `/groups/foundations`, `/groups/navigation`, `/activity`, `/automation`, `/prompt-factory`, `/projects`, `/prompt-gallery`, `/resources`, `/test-lab`, `/validation`, `/settings`, and `/projects/{ProjectId:guid}/structure`.
- Follow with a narrower-width pass on the same route clusters wherever icon sizing, alignment, or overflow might regress.
- Review screenshots for missing glyphs, clipped icons, bad line-height alignment, button or icon centering, treeview chevrons, and toolbar affordance clarity.

## Browser Validation Analytics

- Record each UI-relevant subbundle in `C:/repositories/CanDoItAll/material-icons-migration-bundle-v1/reviews/01-execution-report.md`.
- For every browser row, log route, viewport, actions, screenshot path, and pass or fail result.
- Do not close a UI subbundle with `tested manually` alone; record the actual browser interaction and screenshot review result.

## Working Assumptions

- `CanDoItAll.Components.BaseLib` can safely host local static icon assets that flow through the web application via static web assets.
- Most existing named icon tokens already used in BaseLib and module code can be mapped directly to Google Material icon names or preserved if they already match.
- Shorthand tokens such as `QA`, `PF`, `BR`, and punctuation markers will need explicit mapping decisions instead of blind string carry-over.

## Primary Risks

- Shared renderer changes can silently break icon-only buttons, tabs, or treeview affordances across many routes.
- Workbench and Prompt Factory contain many token sources and raw glyph previews, so a partial migration could leave mixed icon systems in place.
- Existing user edits in Workbench files increase merge risk during the later phases.
