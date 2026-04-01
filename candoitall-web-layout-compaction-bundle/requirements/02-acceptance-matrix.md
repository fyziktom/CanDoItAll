# Acceptance Matrix

| Requirement | Observable Outcome | Primary Proof |
| --- | --- | --- |
| `R-01` | Shell and page surfaces use more width on desktop without creating awkward blank gutters. | Desktop screenshots of `/projects`, `/settings`, and at least one additional operational route |
| `R-02` | `/projects` shows search, status, project, link, and reset controls in one large-screen row. | `/projects` DOM snapshot plus reviewed screenshot at `1720x1160` |
| `R-03` | At least one verbose helper sentence is moved behind a compact help affordance without losing access to the information. | Open-state help affordance proof on `/projects` or another compacted route |
| `R-04` | Other routes adopt the same density rules instead of remaining stacked documentation-style pages. | Route checks for `/dashboard`, `/resources` or `/prompt-gallery`, and `/settings` |
| `R-05` | Project, database, prompt-factory, and workbench dialogs use tighter shells and remain unclipped when open. | Open-state screenshots and action-row checks for each affected modal family |
| `R-06` | Shared input or toolbar primitives stretch naturally and callers constrain them only when needed. | Component/source diff plus route-level checks on projects and at least one list/detail page |
| `R-07` | Styling changes land through Tailwind modules or class composition instead of scattered new raw CSS. | File diff review across `Tailwind/` and component markup |
| `R-08` | Tailwind watch stays active and imported-file edits rebuild `output.css`. | `output/tailwind/watch.stdout.log` plus output stylesheet timestamp change |
| `R-09` | Execution report contains browser analytics rows with route, viewport, actions, screenshots, and result. | `reviews/01-execution-report.md` |
| `R-10` | Every subbundle has a clear checklist, proof contract, and progression gate. | Subbundle README files and prepared-stage validator pass |

