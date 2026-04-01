# Requirement Traceability

| Requirement | Owning Subbundle | Primary Source Files | Planned Proof |
| --- | --- | --- | --- |
| `R-01` | `01`, `02`, `03` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor`, `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Layout\PageScaffold.razor`, `C:\repositories\CanDoItAll\Tailwind\navigation\workbench-shell.css` | Desktop screenshots on `/projects`, `/settings`, and `/dashboard` |
| `R-02` | `02` | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectsBoard.razor` | `/projects` DOM snapshot and screenshot at `1720x1160` |
| `R-03` | `01`, `02`, `03`, `04` | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\HelpPopover.razor`, page files adopting the pattern | Open-state help affordance proof on affected routes |
| `R-04` | `03`, `04` | Main route files in dashboard, resources, prompt gallery, activity, automation, validation, test lab, settings, prompt factory, workbench pages | Multi-route browser analytics rows |
| `R-05` | `02`, `04` | Shared dialog, project modals, database modal, prompt-factory dialogs, workbench overlays | Open-state screenshots and clipping checks |
| `R-06` | `01` | Shared form and toolbar primitives under BaseLib | Component diff plus downstream route checks |
| `R-07` | `01`-`04` | Tailwind imports and class-bearing Razor files | Diff review showing Tailwind-first edits |
| `R-08` | `01` | `C:\repositories\CanDoItAll\Tailwind\input.css`, `C:\repositories\CanDoItAll\output\tailwind\watch.stdout.log` | Watch log and rebuilt `output.css` |
| `R-09` | `05` | `reviews/01-execution-report.md` plus screenshot artifacts | Completed analytics tables and screenshot review notes |
| `R-10` | Bundle-wide | Bundle root and subbundle README files | Prepared-stage validator pass |

