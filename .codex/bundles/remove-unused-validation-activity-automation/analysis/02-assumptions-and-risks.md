# Assumptions And Risks

## Assumptions

- The request is about removing the old module projects and their product surfaces, not banning every ordinary use of the words validation, activity, or automation.
- Existing historical migrations may keep old table definitions as migration history; current runtime composition should stop using those entities.
- SchedulerPlanner remains a supported module and should own any scheduling it still needs after the Automation module is removed.
- The existing `NullActivityStream` is an acceptable explicit sink for non-critical activity emissions after the Activity UI/module is gone.

## Critical Path Risks

- SB01 is the reference boundary: if it misses a direct module reference, later removals can compile but leave a dead UI path or hidden command.
- SB02 is the behavioral extraction: if SchedulerPlanner still depends on Automation, deleting the Automation project will either fail build or silently remove scheduler dispatch.
- SB03 is the deletion and connection cleanup: if Workbench or tests retain obsolete menu/route assumptions, the app can ship broken right-click actions.

## Validation Risks

- Full browser proof depends on the local web host starting on port `5032` after the rebuild.
- Some Playwright tests may require seeded data or browser dependencies that are slower than build-level proof; targeted tests and Browser smoke must still cover navigation regressions.
- Database migration history may create old tables on fresh databases even after runtime modules are removed. That is acceptable only if the app does not register or use those modules.

## Reopen Triggers

- Reopen SB01 if a later `rg` audit finds a remaining direct reference to `CanDoItAll.Modules.Validation`, `CanDoItAll.Modules.Activity`, or `CanDoItAll.Modules.Automation` outside allowed historical artifacts.
- Reopen SB02 if SchedulerPlanner loses the ability to save enabled plans or calculate next planned fires.
- Reopen SB03 if `/validation`, `/activity`, or `/automation` remains in navigation, dashboard cards, workbench quick actions, tests, or project references.
- Reopen SB04 if the rebuilt `5032` host cannot serve the home page and scheduler page after changes.
