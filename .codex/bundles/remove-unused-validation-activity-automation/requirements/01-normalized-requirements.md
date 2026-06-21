# Normalized Requirements

| Requirement | Source | Observable success criteria |
| --- | --- | --- |
| R001 | Main goal | `CanDoItAll.Modules.Validation`, `CanDoItAll.Modules.Activity`, and `CanDoItAll.Modules.Automation` are removed from the solution, project references, runtime registration, and module assembly discovery. |
| R002 | "remove also tests" | Tests that directly exercise the removed modules are deleted; tests with incidental references are updated to assert the remaining supported behavior. |
| R003 | "surgical precise" | A reference workbook exists before product code edits and each direct reference category maps to an explicit removal or keep decision. |
| R004 | "multiple places like project structure right click menu" | Workbench/project-structure menu items, quick actions, projections, scope bridges, and command routing no longer expose the old Validation module. |
| R005 | "calendar-scheduler ... covers automation tasks" | SchedulerPlanner no longer depends on the old Automation module and remains buildable and navigable. |
| R006 | "validation and activity I never use" | Validation and Activity routes, navigation entries, dashboard cards, module services, and obsolete activity/validation tests are removed. |
| R007 | "app is working again" | Build succeeds, relevant tests run or explicit test gaps are recorded, and Browser proof shows the app starts after restart. |
| R008 | "rebuild our running 5032 instance" | Existing port `5032` process is stopped before edits; after build, a fresh web host is started on port `5032` and smoke-tested. |
| R009 | Scope control | Generic validation, activity, or automation terminology remains when it belongs to unrelated scheduler, workflow, process, cognitive memory, domain validation, or historical migration behavior. |
