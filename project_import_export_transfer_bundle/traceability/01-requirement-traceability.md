# Requirement Traceability

| Requirement | Raw notes | Owning subbundle | Bundle destinations | Planned proof |
| --- | --- | --- | --- | --- |
| `R001` | `N001`, `N003`, `N004` | `01-project-database-transfer` | `analysis/01-current-state.md`, `architecture/01-target-solution.md`, `subbundles/01-01-project-database-transfer/README.md` | Integration test for profile-to-profile transfer. |
| `R002` | `N001` | `01-project-database-transfer` | `inventories/01-scope-inventory.md`, `subbundles/01-01-project-database-transfer/README.md` | Assertions for project hierarchy, nodes, links, bindings, references, view state, and layouts. |
| `R003` | `N001`, `N002` | `02-project-zip-package-import-export` | `architecture/01-target-solution.md`, `subbundles/02-02-project-zip-package-import-export/README.md` | Package file created with manifest and table payloads. |
| `R004` | `N001`, `N002` | `02-project-zip-package-import-export` | `subbundles/02-02-project-zip-package-import-export/README.md` | Import package into empty target and verify loaded projects/workbench graph. |
| `R005` | `N003`, `N004` | `03-ui-exposure-and-workflow-proof` | `subbundles/03-03-ui-exposure-and-workflow-proof/README.md` | Browser proof on settings/startup transfer UI showing `Projects`. |
| `R006` | `N002` | `03-ui-exposure-and-workflow-proof` | `subbundles/03-03-ui-exposure-and-workflow-proof/README.md` | Browser proof on `/projects` showing zip export/import controls. |
| `R007` | `N004` | `04-regression-and-closure` | `subbundles/04-04-regression-and-closure/README.md`, `reviews/01-execution-report.md` | Targeted tests/build confirming existing transfer handlers still work. |

## Raw Note Closure Matrix

| Raw note | Exact wording | Requirements | Owning subbundles | Planned proof | Exception status |
| --- | --- | --- | --- | --- | --- |
| `N001` | `add system for export all projects and import all projects` | `R001`, `R002`, `R003`, `R004` | `01`, `02` | DB-transfer and package import/export tests | No exception. |
| `N002` | `must work as zip import/export` | `R003`, `R004`, `R006` | `02`, `03` | Package tests and UI proof | No exception. |
| `N003` | `also transfer between existing dbs just via UI` | `R001`, `R005` | `01`, `03` | Handler registration plus browser proof | No exception. |
| `N004` | `Same transfer can work for transfer of processes, agents, etc. similar as we have it now when creating new database.` | `R001`, `R005`, `R007` | `01`, `03`, `04` | Existing transfer UI/checklist still lists handlers; no regression in tests | No exception. |
