# Assumptions And Risks

## Working Assumptions

- The correct executable scope for this run is the live unresolved branching gap plus the bundle repair needed to execute it honestly.
- The current single incoming dependency field can be extended safely for switch-style branching in this pass without introducing full multi-predecessor join semantics.
- Decision-maker role ownership will be modeled as a typed role reference and not inferred from free text.
- Existing audit items that are now materially implemented do not need to be reopened just to mimic the stale spreadsheet.

## Critical Path Risks

- If the definition-side branch model is weak, every downstream runtime, MCP, and UI proof becomes untrustworthy.
- If non-selected branch paths are not skipped or otherwise resolved deterministically, runs may never close correctly.
- If export, import, clone, and publish validation do not understand the new branch data, authoring and runtime may drift apart across versions.

## Validation Risks

- UI proof can be weak if the browser session does not validate both authoring and runtime branch flows.
- Targeted tests may pass while broader build integration fails if migrations or MCP contracts drift.
- Browser proof can be misleading if only the closed state is captured and branch selectors or panels are never opened.

## Reopen Triggers

- Reopen subbundle 02 if runtime work reveals the chosen definition model cannot express required switch-style routing cleanly.
- Reopen subbundle 03 if UI proof reveals the runtime contract still cannot expose available branch choices safely.
- Reopen subbundle 04 if Playwright proof shows authoring or runtime branch flows are confusing, clipped, or impossible to complete.
- Reopen the whole bundle if prepared-stage or completed-stage validation fails after material bundle edits.
