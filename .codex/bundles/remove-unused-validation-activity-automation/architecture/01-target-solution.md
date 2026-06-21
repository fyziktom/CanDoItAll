# Target Solution

## Runtime Shape

- Composition no longer calls `AddValidationModule`, `AddActivityModule`, or `AddAutomationModule`.
- Module assembly discovery no longer includes marker assemblies from the three removed modules.
- The web project no longer references or imports those modules.
- The old `/validation`, `/activity`, and `/automation` routes are not advertised through shell navigation, dashboard cards, database-profile redirects, workspace shortcuts, or workbench quick actions.

## Scheduler Replacement Boundary

SchedulerPlanner keeps its calendar/schedule planning surface, but it must stop depending on `CanDoItAll.Modules.Automation`. The replacement belongs inside SchedulerPlanner or an already-supported infrastructure boundary, with strongly typed scheduling concepts and explicit failure behavior. Workflows and processes remain the higher-level automation path.

## Workbench Boundary

Project structure should retain project, process, workflow, test, and scheduler surfaces. It should not create or project ValidationRun nodes from the removed module. Generic canvas validation and health overlays may remain when they do not depend on the removed Validation module.

## Data Boundary

Historical EF migrations are not the primary removal surface. Runtime model registration and project references must be removed first. A schema-drop migration is only required if build/runtime validation proves the current migrations or model snapshot block app startup.
