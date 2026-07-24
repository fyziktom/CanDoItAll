# Current State

## Evidence Method

- Repository source, project files, existing component tests, Playwright tests, and preserved feedback images were inspected directly on 2026-07-24.
- CodeAnalytics MCP transport was unavailable. No snapshot id, dashboard health, automated finding list, or dependency-cycle result exists.
- Components MCP was intermittent. A parallel recommendation succeeded and selected BaseLib `Dialog`, form wrappers, `DataGrid`, `ListDetailShell`, and typed `CdaChart`; this agent's later `components_libraries_list` call failed with `Transport closed`. Project XML, exact source symbols, existing usage examples, and tests are the setup fallback.

## Architectural Hotspots

| Source | Size | Current ownership | Risk |
| --- | ---: | --- | --- |
| `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs` | 6,054 lines | Editor/read models plus `PartyDirectoryService`, `CrmService`, `HrService`, `AiAgentService`, and `ProjectPartyIntegrationService`. | New picker, financial, or UI-projection logic added here would increase responsibility concentration. |
| `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrCrmPage.razor` | 1,810 lines | Account list, route reconciliation, interactions, opportunity filters/list/editor/conversion state, notifications, and navigation. | Opportunity and Financials work can easily create another page-local subsystem. |
| `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrDirectoryPage.razor` | 1,866 lines | Directory list/filtering, tabbed party editor, load generations, mapping, merge/import/export orchestration, and notifications. | Contact wizard and cross-form picker work must not become more nested page state. |

`PartyDirectoryService`, `CrmService`, `HrService`, and `AiAgentService` are partial classes. Their cross-module search/index behavior is continued in `repo://src/Modules/CanDoItAll.Modules.CrmHr/Integration/CrmHrCrossModuleIntegration.cs`. This is an existing partial split, not permission to add feature partials.

## Constructor And Composition Evidence

| Type | Constructor dependencies | Registration/consumer evidence |
| --- | ---: | --- |
| `PartyDirectoryService` | 4: `IDbContextFactory<AppDbContext>`, `IClock`, `IActivityStream`, `ISearchIndexService` | Scoped in `CrmHrModuleServiceCollectionExtensions`; injected by Directory page and other services. |
| `CrmService` | 6: the four above plus `ProjectsService`, `IProjectPartyIntegrationBridge` | Scoped; injected by CRM page. |
| `HrService` | 4 | Scoped; injected by Workforce surfaces. |
| `AiAgentService` | 6: persistence/clock/activity/search plus `PartyDirectoryService`, `IAiTechnicalAgentBridge` | Scoped; injected by Agents surfaces. |
| `ProjectPartyIntegrationService` | 4 | Scoped and exposed through two project integration bridge interfaces. |
| `CrmHrCrmPage` | 5 injected services | Directly creates page state/editor models and owns opportunity filtering. |
| `CrmHrDirectoryPage` | 5 injected services | Directly creates and maps its nested `PartyEditorViewModel`. |

Direct construction is mostly editor/view state (`new CrmOpportunityEditorModel`, contact/relationship rows, page load-generation helpers). Runtime services are composed through `AddCrmHrModule`. New domain queries should follow DI composition; UI selection state should remain component-owned and independently testable.

## Current UI Behavior Versus Feedback

### Tags

- Directory party tags are edited with `InputText` bound to `TagsText`, then split by comma in `PartyEditorViewModel.ToEditorModel`.
- No CRM/HR `TagEditor` usage exists, although BaseLib `TagEditor` is used in Prompts, Memory, Scheduler, and AgentFramework.
- Party-level tags persist as `Party.TagsJson`. `PartyContactPoint` currently has no tag property.

### Contact methods and relationships

- `PartyContactMethodsEditor.razor` appends a mutable row immediately. Its `for`-loop lambda captures the mutable `index`; after the loop advances, a one-row Remove can call `RemoveAt(1)` on a one-item list.
- The same closure defect exists in `PartyAddressesEditor.razor` and in relationship direction/remove callbacks in `PartyRelationshipsEditor.razor`. Existing safe precedents copy `rowIndex` before creating the callback.
- Empty additional contacts are already filtered before save, so the reported failure is callback identity, not blank-contact persistence.
- `PartyRelationshipsEditor.razor` also loads all parties, sorts in memory, and renders a normal `<InputSelect>`.

### Record selection and standard lists

- `PartyPicker.razor` is a dropdown over a fully materialized `Options` list, followed by an optional inline quick-create panel.
- `ResourceCardPicker<TItem>` in `CanDoItAll.AppComponents` already provides strongly typed card selection and search, and is used by Project Structure task/resource dialogs. It filters a fully supplied list in memory and has no server paging, total count, tag-filter control, or async error contract.
- Prompts has the strongest data-bound precedent: `PromptGallerySearchList` combines typed filters, `TagEditor`, loading/error/empty states, `DataGrid`, pager, debounce/cancellation, server loading, and stale-request guards; `PromptGalleryQuery`/`PromptGalleryPage<T>` validate typed requests; `EfPromptGallerySearchDriver` performs `Count/Skip/Take`; `PromptGalleryPickerDialog` hosts the same search surface in a dialog.
- `PagedCardGrid<TItem>` pages a fully supplied in-memory list; it is not a scalable data boundary.
- CRM/HR routed pages use `ListDetailShell`, `FilterBar`, and searchable lists, but their source lists are loaded wholesale and filtered in page memory.

### Opportunities

- `OpportunityBoard.razor` is already a reusable visual component, but CRM page owns filtering and renders six controls vertically.
- `OpportunityBoard` currently sums weighted amount across all currencies in a stage and renders the number without a currency. That existing mixed-currency defect must be removed or grouped before SB04/SB05 can claim currency-safe behavior.
- Owner, delivery-unit, and related-party inputs are ordinary dropdowns populated from loaded account data.
- The page renders `OpportunityBoard` and the 339-line `OpportunityEditor` as stacked permanent surfaces.
- Opportunity creation is immediate editor initialization, not a wizard. Selecting a card updates route/query state and the stacked editor; it does not open a detail dialog.
- `CrmOpportunityEditorModel.LinkedProjectId` exists and conversion can link an existing project, but normal create/edit has no reusable project picker.

### Financials

- CRM account detail tabs are Overview, Stakeholders, Interactions, and Opportunities. No Financials tab or financial query projection exists.
- `Opportunity` has amount, currency, expected close date, probability, and stage. Won opportunities can support a sold projection.
- No purchase-order/purchase opportunity or invoice entity exists. Bought and overdue-invoice values cannot be honestly computed.
- `CanDoItAll.Components.Charts` 0.1.4 and host assets already exist in the web app; AgentFramework's `ProviderUsageDialog.razor` demonstrates bar and donut `CdaChart` usage. CRM/HR does not currently reference/import Charts.

### Workspace tab titles

- CRM/HR pages already have distinct browser `PageTitle` values.
- Workbench tabs are built in `MainLayout.Workbench.cs` from `ShellNavigation.MatchRoute`. The only registered base item is `/crm-hr` titled `CRM / HR`, so every CRM/HR subroute resolves to the same workbench title.
- `IShellNavigationContributor` is used by Memory, Processes, and AgentFramework, but `ShellNavigation.BuildItems` flattens contributions into the actual main navigation and currently ignores `IsSubItem`. It is unsafe to register six CRM child entries only to obtain tab titles.

## Current Dependency Direction

- `CanDoItAll.Modules.CrmHr` references BaseLib, Gantt, Projects, Workspace, Infrastructure, SharedKernel, MAF common projects, and Memory abstractions/application.
- It does not reference `CanDoItAll.AppComponents` or Charts.
- `CanDoItAll.AppComponents` references BaseLib/Common/CanvasLib, SharedKernel, and FileTools abstractions; it has no dependency on CRM/HR or Projects.
- Adding `CrmHr -> AppComponents` is directionally possible from the inspected project files. The build/checkpoint must still prove no cycle.
- CRM/HR already references Projects; a project query adapter can remain in Projects and be consumed from CRM/HR without reversing that direction.

## Existing Test Surfaces

- Components: `CrmHrDirectoryPageFreshnessTests`, `CrmHrSecondaryPageFreshnessTests`, `CrmHrWorkspaceFreshnessTests`, `CrmHrNavigationTests`, `ProjectsCrmHrIntegrationTests`, `ProjectStructurePartyPickerTests`, `ResourceCardPickerTests`, and `ListDetailShellTests`.
- Playwright: `CrmHrDirectoryFlowTests`, `CrmInteractionFlowTests`, `CrmHrWorkforceFlowTests`, `CrmHrRegressionTests`, `CrmHrShellSmokeTests`, and related privacy/cross-module flows.
- Integration/unit: CRM schema, interactions, cross-module, audit, agent-query, and load-generation tests.
- Missing: server-paged picker contract/query tests, >1,000-record proof, tag/type combination proof, contact empty-cancel/remove regression, contact tag persistence/migration proof, opportunity dialog flow proof, project picker proof, financial availability/aggregation proof, and contextual workbench-title proof.

## Current-State Conclusion

The repository has useful visual and composition primitives, including a complete server-paged Prompts precedent, but CRM/HR selection and list data paths remain in-memory and page-owned. The smallest architecture-safe route is to extract a neutral typed paged browser into AppComponents, add module-owned query adapters, isolate dialog workflow state, add a dedicated financial read projection, and use a typed CRM route catalog for workbench metadata—without adding new partial files or embedding reusable logic in the two large pages.
