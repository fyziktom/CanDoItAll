# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `R001` project-structure workflow runs | `requirements/01-normalized-requirements.md` | `subbundles/01`, `subbundles/03`, `subbundles/04`, `subbundles/07` | Backend tests, API tests, Playwright start proof | Main outcome |
| `R002` explicit workflow nodes | `architecture/01-target-solution.md` | `subbundles/01`, `subbundles/02` | Node create/read tests | Must avoid loose note metadata |
| `R003` workflow-selection dialog | `inputs/02-structured-input.md` | `subbundles/02`, `subbundles/04` | Component tests and browser proof | Add flow |
| `R004` advanced input setup | `architecture/01-target-solution.md` | `subbundles/02` | Input preview tests and screenshot | Critical foundation |
| `R005` always include project/parent details | `requirements/02-input-coverage-matrix.md` | `subbundles/02`, `subbundles/03` | Snapshot tests | Absolute language from raw input |
| `R006` confirmation start without matching resources | `inputs/02-structured-input.md` | `subbundles/03`, `subbundles/04` | UI assertion and screenshot | Must not copy process staffing flow |
| `R007` status/progress/markers | `architecture/01-target-solution.md` | `subbundles/03` | State mapping tests | Critical foundation |
| `R008` selection floating status | `architecture/01-target-solution.md` | `subbundles/03`, `subbundles/04` | Component tests and screenshot | Includes step count |
| `R009` result nodes under workflow node | `architecture/01-target-solution.md` | `subbundles/05` | Projection tests | Critical foundation |
| `R010` execution summary with file paths | `architecture/01-target-solution.md` | `subbundles/05`, `subbundles/06` | Summary tests and scenario proof | Includes non-asset file paths |
| `R011` backend before UI | `plan/01-phase-plan.md` | `subbundles/01`, `subbundles/03`, `subbundles/04` | Gate rows in execution report | Sequencing rule |
| `R012` at least 20 cases | `templates/01-scenario-matrix.md` | `subbundles/06`, `subbundles/07` | Scenario result file | Closure blocker |
| `R013` supplied and synthetic data | `inputs/01-source-artifacts.md` | `subbundles/06` | Scenario harness artifacts | Mouser/SEAMARK/financial/synthetic |
| `R014` PostgreSQL and providers | `inputs/02-structured-input.md` | `subbundles/07` | Provider/database proof | Required by user |
| `R015` repair subbundle on trouble | `analysis/02-assumptions-and-risks.md` | `subbundles/07` | Reopen/repair notes | Must not hide defects |
