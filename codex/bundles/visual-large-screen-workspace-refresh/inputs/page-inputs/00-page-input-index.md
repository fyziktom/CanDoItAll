# Page Input Index

These page inputs are based on the current Razor implementation, route wrappers, shared components, and dialogs found under `C:\repositories\CanDoItAll\src`. They are large-screen-only inputs for the visual refresh bundle.

## Source Scan

- Route pages were identified from `@page` directives in the primary Blazor host and product modules.
- Current elements were extracted from real `PageScaffold`, `SummaryTile`, `TabsItem`, `SecondaryTabs`, `FormSection`, `Dialog`, `DialogScaffold`, `TreeView`, `Button`, `EmptyState`, and module component usage.
- Thin route wrappers are documented with their backing real component. For example `/processes` and `/projects/{ProjectId}/processes` both render `ProcessWorkspace`.
- Generated images are planning proposals only. They do not replace runtime screenshots, Playwright proof, or accessibility/interaction verification.

## Imagegen Proposal Boards

| Proposal asset | Covers |
|---|---|
| `evidence/design-proposals/pages/01-shell-baselib-corrected-proposal.png` | Shared shell, collapsed rail, tooltip, bottom Settings/DB actions, DB flyout, full-width workspace. |
| `evidence/design-proposals/pages/02-project-pages-tabs-dialogs-proposal.png` | Projects, project modal wizard, hierarchy modal, Gantt dialog, project structure, calendar. |
| `evidence/design-proposals/pages/03-process-pages-tabs-dialogs-proposal.png` | Process workspace tabs, nested runs tabs, process dialogs, live process tabs and dialogs. |
| `evidence/design-proposals/pages/04-agent-workflow-tabs-dialogs-proposal.png` | Agents tabs, agent details dialog tabs, workflow tabs, workflow dialogs. |
| `evidence/design-proposals/pages/05-core-pages-tabs-dialogs-proposal.png` | Dashboard, resources, plugins tabs/dialogs, prompt gallery, prompt factory tabs/dialogs, settings/DB transfer. |
| `evidence/design-proposals/pages/06-supporting-pages-tabs-dialogs-proposal.png` | CRM/HR pages, collaboration tabs, activity, automation, scheduler tabs/dialog, validation, test lab. |
| `evidence/design-proposals/pages/07-baselib-reusable-components-proposal.png` | Reusable BaseLib component candidates used by the page proposals. |

## Page Input Files

| Input file | Page inputs |
|---|---|
| `01-shell-dashboard-projects.md` | App shell, dashboard, projects, project structure, project calendar. |
| `02-processes-live.md` | Process workspace routes, process tabs, nested runs tabs, process dialogs, live processes dashboard. |
| `03-agents-workflows.md` | Agents page tabs, agent dialogs, workflows page tabs, workflow dialogs. |
| `04-prompts-plugins-settings-resources.md` | Prompt factory, prompt gallery, plugins, resources, settings, database settings panel. |
| `05-crm-hr.md` | CRM/HR hub, directory, CRM, workforce, recruiting, agents, assignments, CRM/HR dialogs. |
| `06-operations-supporting.md` | Collaboration, activity, automation, scheduler, validation, test lab, supporting dialogs. |

## Coverage Rule

Every implementation subbundle must preserve the real functions listed in these inputs. If a runtime screenshot reveals that an image proposal cannot cover a function, the owning subbundle must repair the proposal or document a stricter implementation instruction before coding continues.
