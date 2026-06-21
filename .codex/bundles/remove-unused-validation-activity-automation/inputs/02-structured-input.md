# Structured Input

## Core Objective

- Remove the old Validation, Activity, and Automation modules from the product and tests while preserving supported scheduler, workflow, process, and project-structure paths.

## Success Criteria

- The three old module projects are not in the solution, web project, composition project, test support, or runtime registration.
- `/validation`, `/activity`, and `/automation` are not advertised through navigation, dashboard, layout shortcuts, or Workbench commands.
- Tests that directly target the removed modules are deleted or rewritten to supported behavior.
- SchedulerPlanner builds and remains navigable without importing `CanDoItAll.Modules.Automation`.
- The app rebuilds, restarts on port `5032`, and passes Browser smoke validation.

## Hard Constraints

- Stop the running port `5032` instance before product edits.
- Create an XLSX reference map before deleting references.
- Keep changes surgical and avoid broad refactors.
- Keep generic validation/activity/automation behavior when it is unrelated to the removed modules.

## Allowed Side Effects

- Remove obsolete projects, routes, UI entries, service registrations, project references, and related tests.
- Refactor SchedulerPlanner only as needed to remove its old Automation module dependency.
- Leave historical migration metadata in place unless it breaks build or runtime validation.

## Source Artifacts

- `bundle://inventories/unused-module-reference-map.xlsx`
- `repo://src`
- `repo://tests`
- `repo://CanDoItAll.slnx`

## Input Coverage Signals

- Main goal names all three modules and must not be narrowed to only web navigation cleanup.
- The right-click/project-structure note must be explicitly covered.
- The related-tests note must be explicitly covered.
- The port `5032` stop/rebuild/test note must be explicitly covered.

## Dependency And Sequencing Signals

- Reference mapping must precede deletion.
- SchedulerPlanner dependency extraction must precede Automation project deletion.
- Project/test removal must precede build and Browser validation.

## Validation Expectations

- Prepared and completed bundle validator runs.
- Targeted direct-reference audit after deletion.
- Build and test transcripts with exit codes.
- Browser smoke proof against the restarted port `5032` host.

## Evidence Contract

- `bundle://inventories/unused-module-reference-map.xlsx`
- `bundle://proof/SB03/transcripts/direct-reference-audit.txt`
- `bundle://proof/SB04/transcripts/build.txt`
- `bundle://proof/SB04/transcripts/tests.txt`
- `bundle://proof/SB04/transcripts/port-5032-restart.txt`
- Browser screenshots or DOM proof for home and scheduler routes.

## UI Validation Strategy

- Open the restarted app on a desktop viewport and verify primary navigation no longer exposes Validation, Activity, or Automation module routes.
- Open the scheduler route to prove the replacement surface still renders.
- Use a narrower viewport follow-up if layout changes affect navigation wrapping.

## Browser Validation Analytics

- SB04 records route, viewport, Browser actions, screenshots, and result rows in `reviews/01-execution-report.md`.

## Working Assumptions

- The request targets the old module projects and their product surfaces, not every generic use of the three words.
- SchedulerPlanner remains supported and should own schedule dispatch without the old Automation module.

## Primary Risks

- Hidden Workbench/project-structure references can survive a simple project-reference removal.
- SchedulerPlanner currently imports old Automation contracts and needs careful extraction.
- Tests may encode old cross-module activity assumptions that should be removed without weakening unrelated business behavior.
