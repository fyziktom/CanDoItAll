# Shared models, components, and services inventory

## BaseLib assets approved for CRM/HR UI

| Category | BaseLib assets | Reuse decision | Risk note |
| --- | --- | --- | --- |
| Page layout | PageScaffold, PageHeader, SummaryTiles, SummaryTile, SectionCard | Use for every CRM/HR route header and summary region. | Safe |
| Navigation | SecondaryTabs, FilterBar, Toolbar, ToolbarRow | Use for Directory / CRM / HR / AI Agents / Assignments route tabs and filters. | Safe |
| Lists | ListDetailShell, ListPanelHeader, SelectionListItem, FactTable, MetaList | Use for directory, workforce, recruiting, and assignment screens. | Safe |
| Forms | FormSection, FormRow, TextBox, TextArea, DropDown, CheckBox, Switch, Numeric, TagEditor | Use for detail editors and filters. | Safe |
| Feedback | EmptyState, Alert, Callout, LoadingState | Use for empty registries, privacy warnings, merge guidance, and loading. | Safe |
| Identity | Avatar, Icon, StatusBadge | Use for person/company/agent cards and status markers. | Safe |
| Modals | Dialog | Use sparingly. Documentation says it is a placeholder rather than a rich overlay system; prefer inline list/detail or page-level shells when possible. | Caution |
| Data grids | DataGrid | Avoid for anything that needs advanced sorting/filtering/grouping; use simpler list/detail layouts or small tables. | Caution |
| Charts | Chart, LineSeries | Only use for simple trend visuals if needed; do not rely on advanced chart features. | Caution |

## Shared runtime and model assets to reuse

| Shared part | Path | Current value | CRM/HR reuse |
| --- | --- | --- | --- |
| Search index | src/CanDoItAll.Infrastructure/Search/SearchIndexing.cs | Cross-entity search index with source type/key semantics. | Index parties, opportunities, candidate records, allocations, and agents. |
| Activity stream | src/CanDoItAll.Modules.Activity/ActivityModels.cs | Timeline persistence and search bridge. | Record create/update/archive/convert/assign events. |
| Projects service | src/CanDoItAll.Modules.Projects/ProjectModels.cs | Project aggregate and hierarchy support. | Link opportunity/customer/delivery unit/assignments to projects. |
| Workbench service | src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs | Project structure persistence and node mutation workflows. | Bridge participant nodes and task assignments to central parties. |
| Workbench participant metadata | src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs | Existing lightweight participant, meeting, and assignee metadata. | Add PartyId references rather than replacing node semantics. |
| Resources service | src/CanDoItAll.Modules.Resources/ResourceModels.cs | Typed resource registry. | Add owner/maintainer party links. |
| Workspace provider profiles | src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs | Provider configuration and health checks. | Attach AI agents to provider profiles and default models. |
| Security / secrets | src/CanDoItAll.Modules.Security/SecurityModels.cs | Secret storage and protection. | Reuse for any AI-agent or CRM connector secrets later, do not duplicate. |
| Validation service | src/CanDoItAll.Modules.Validation/ValidationModels.cs | Rule-first validation runs and findings. | Allow reviewer/owner party links. |
| TestLab service | src/CanDoItAll.Modules.TestLab/TestLabModels.cs | Test plan, cases, evidence, runs. | Allow owner/reviewer party links. |
| Playwright fixture | tests/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs | Starts app and temp DB for browser tests. | Primary repo-native E2E validation base. |


## Explicit UI rule

The CRM/HR module must **not** use:

- `CanDoItAll.Components.CanvasLib`
- floating canvas workbench hosts
- project-structure canvas primitives
- prompt-factory toolbox or canvas overlays

The module **may** use:

- `CanDoItAll.Components.BaseLib`
- normal Razor components
- standard Tailwind utility classes already present in the repository
- simple inline dialogs only when BaseLib limitations do not block usability
