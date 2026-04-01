# Assumptions And Risks

## Assumptions

- The requested right-click keyboard behavior is intended for the shared `CanvasWorkbench` runtime as exercised on the project-structure route, not a one-off page-local menu implementation.
- The architect-provided shortcut letters are fixed requirements for the named groups and named leaves.
- Unlisted siblings should still receive shortcuts, but those can be resolved through a deterministic collision-free assignment strategy rather than a manually curated list for every current and future leaf.
- The help modal can use tabs, segmented buttons, or another lightweight page-switching pattern as long as the result feels like browsable basic docs rather than one flat wall of text.
- If splitting `03-interaction-and-state.js` creates asset-load-order risk, extracting only the shortcut-specific helpers into a focused runtime module is acceptable as the maintainability response.

## Critical Path Risks

- Shortcut collisions are likely because several sibling sets are already large, especially the block catalog and node-action menus. Weak collision handling would make the keyboard map inconsistent or misleading.
- Runtime ownership is split across `03`, `04`, and `05` JS modules. A shortcut refactor that ignores load order or shared exports could break unrelated pointer and selection behavior.
- The project-structure catalog is large and partially generated from helper methods. A shortcut change that lives only in one adapter branch would drift from the rest of the menu tree quickly.
- The help modal must stay synchronized with the shipped shortcut contract. Hand-written docs that diverge from the rendered menu would turn the new discoverability feature into a support burden.

## Validation Risks

- bUnit coverage can validate rendered help content and action metadata, but it cannot prove real browser keyboard routing through nested context-menu layers.
- Playwright proof must open the actual menu and verify the open submenu state, not only the closed toolbar or page shell.
- Asset-backed create leaves such as PDF and Excel may require stable test data for browser proof if the shortcut path needs to exercise real create flows.
- If the maintainability refactor introduces a new runtime file, the proof must confirm the file actually loads on the route instead of silently relying on cached or stale assets.

## Reopen Triggers

- Any sibling menu layer still has duplicate accelerator keys after the final assignment step.
- Pressing a documented shortcut opens the wrong submenu, does nothing, or executes the wrong leaf.
- The underlined shortcut character does not match the actual key the runtime listens for.
- The help modal shows a shortcut page, but its content no longer matches the rendered menu labels or nested mappings.
- The maintainability refactor leaves `03-interaction-and-state.js` larger because new shortcut logic was duplicated instead of extracted.
- Playwright proof cannot keep the submenu open long enough to verify clipping, layering, or nested keyboard progression.
