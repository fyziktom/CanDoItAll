# Normalized Requirements

| Requirement | Description | Source |
| --- | --- | --- |
| `R001` | Persist direct project-to-project hierarchy relations so any project can be a parent of many projects and a child of many projects. | `N001`, `N002`, `N003` |
| `R002` | Support arbitrary-depth hierarchy traversal and typed lookup of direct parents, direct children, ancestors, and descendants without duplicating project rows. | `N004`, `N005` |
| `R003` | Reject self-parent and cyclic hierarchy operations explicitly so the structure stays navigable and the workbench sync cannot recurse into invalid graphs. | `N012` |
| `R004` | Extend project service/query models with hierarchy metadata needed by both the Projects page and structure-canvas projection. | `N005`, `N007`, `N008` |
| `R005` | The Projects page must let the user filter to a project's direct subprojects and discover the parent projects of a selected project. | `N005` |
| `R006` | Each project card must expose a hierarchy affordance that opens direct subprojects in a modal card surface. | `N006` |
| `R007` | The subproject modal must support recursive drill-in and clearly indicate when a listed subproject also belongs to other parents. | `N006`, `N011`, `N012` |
| `R008` | Related-project navigation from `/projects` must preserve or reuse the existing detail, dashboard, structure, and calendar flows instead of creating a disconnected hierarchy-only island. | `N005`, `N006`, `N012` |
| `R009` | A project's structure canvas must render its direct subprojects as related project nodes with visible relation lines. | `N007` |
| `R010` | A project's structure canvas must render its direct parent projects when they exist. | `N008` |
| `R011` | If a displayed subproject has an additional parent outside the current branch, that other parent must also render as a subdued, disabled-looking, but still openable related-project node. | `N011` |
| `R012` | Canvas actions must allow adding a subproject relation and opening the related project's structure canvas in a new browser tab. | `N009` |
| `R013` | Canvas actions must allow reconnecting a related project beneath another project without leaving stale relations or broken navigation. | `N010`, `N012` |
| `R014` | Automated tests must cover hierarchy persistence, cycle rules, Projects page hierarchy UI, structure-canvas projection, and existing project/workbench regressions touched by the change. | `N012`, `N013` |
| `R015` | Headed Playwright validation and screenshot review must prove the hierarchy behavior on `/projects` and `/projects/{id}/structure` before the bundle can close. | `N013` |
| `R016` | Analytics captured in `candoitall-skill-analytics` must drive concrete repo skill changes, and the install/reinstall scripts must distribute those updated skills to other machines. | `N014` |
