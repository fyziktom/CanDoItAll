# C# Current-State Inventory

## Evidence Status

- Inspection date: 2026-07-24.
- CodeAnalytics MCP transport: unavailable.
- Components MCP transport: intermittent; one parallel recommendation succeeded, while a later library-list call returned `Transport closed`.
- CodeAnalytics snapshot/dashboard/findings/dependency-cycle evidence: not available; this is a validation gap, not evidence that no issues exist.
- Component recommendation evidence: BaseLib `Dialog`, form wrappers, `DataGrid`, `ListDetailShell`, and typed `CdaChart`; complete library/setup retrieval remains unavailable.
- Substitute evidence: exact source inspection, project XML, `rg` symbol/reference searches, existing test inventory, and line counts. Implementation gates must add build/project-reference proof and may add a scoped CodeAnalytics snapshot only if transport is restored.

## Source Files Inspected

- `repo://src/Modules/CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrModuleServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Integration/CrmHrCrossModuleIntegration.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Models/CrmHrFoundationModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Models/CrmHrBusinessModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrDirectoryPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrCrmPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/PartyPicker.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/PagedCardGrid.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/CompactFilterBar.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/PartyContactMethodsEditor.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/PartyRelationshipsEditor.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/OpportunityBoard.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/OpportunityEditor.razor`
- `repo://src/UI/CanDoItAll.AppComponents/Components/ResourceCardPicker.razor`
- `repo://src/UI/CanDoItAll.AppComponents/Components/ResourceCardPickerOption.cs`
- `repo://src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.Projects/Pages/Components/ProjectsBoard.razor`
- `repo://src/App/CanDoItAll.Web/Components/Layout/MainLayout.Workbench.cs`
- `repo://src/App/CanDoItAll.Web/Composition/ShellNavigation.cs`

## Large And Partial Types

| Type/surface | Size | Partial state | Responsibilities observed |
| --- | ---: | --- | --- |
| `CrmHrServices.cs` | 6,054 lines | Contains four partial services. | Editor/read models, directory, CRM, HR, AI agents, project-party integration, validation, mapping, persistence, activity/search side effects. |
| `PartyDirectoryService` | starts at line 733 | Continued in `CrmHrCrossModuleIntegration.cs`. | Directory reads/writes plus cross-module search/index behavior. |
| `CrmService` | starts at line 1,156 | Continued in `CrmHrCrossModuleIntegration.cs`. | Account/interaction/opportunity reads/writes, route resolution, conversion, project integration, indexing. |
| `HrService` | starts at line 2,800 | Continued in `CrmHrCrossModuleIntegration.cs`. | Workforce/capacity/staffing operations plus indexing. |
| `AiAgentService` | starts at line 4,115 | Continued in `CrmHrCrossModuleIntegration.cs`. | AI party projections, staffing facts, technical-agent bridge, indexing. |
| `CrmHrCrmPage.razor` | 1,810 lines | Not partial. | Account/detail tabs, route generations, filters, opportunity editing, conversion, notifications, navigation. |
| `CrmHrDirectoryPage.razor` | 1,866 lines | Not partial. | Directory list, nested editor view model, tabs, load generations, merge/import/export, notifications. |

The existing partial-class split is cross-module integration leakage already under risk. No new feature partials are allowed.

## Constructor Dependency Counts

| Type | Count | Dependencies |
| --- | ---: | --- |
| `PartyDirectoryService` | 4 | DB context factory, clock, activity stream, search index. |
| `CrmService` | 6 | The four above, `ProjectsService`, project-party integration bridge. |
| `HrService` | 4 | DB context factory, clock, activity stream, search index. |
| `AiAgentService` | 6 | DB context factory, clock, activity stream, search index, directory service, AI bridge. |
| `ProjectPartyIntegrationService` | 4 | DB context factory, directory service, assignment policy, project mutation bridge. |
| `CrmHrCrmPage` | 5 injected | CRM, Projects, agent chat, navigation, notifications. |
| `CrmHrDirectoryPage` | 5 injected | Directory, management, navigation, notifications, logger. |

## Direct Instantiation And Composition Points

- `AddCrmHrModule` registers the services above as scoped and exposes project integration through two interfaces.
- CRM/HR pages use DI for runtime services but directly construct editor models, list rows, and load-generation state.
- `PartyContactMethodsEditor` directly constructs an empty contact and mutates the live list before validation.
- CRM page directly constructs opportunity editor/conversion models and owns all opportunity filter predicates.
- AppComponents `ResourceCardPicker<TItem>` owns typed selection state but receives a fully materialized list.

## Provider, Tool, Driver, And Memory Responsibilities

- No new provider/tool/driver protocol is introduced by this bundle.
- Existing CRM/HR services publish activity/search effects and memory source snapshots. Contact-tag persistence must update or explicitly preserve snapshot/redaction/import/export behavior.
- Picker query services are read-side application/infrastructure adapters, not new memory/provider abstractions.
- Financials is a read projection only; it must not own opportunity writes or external accounting providers.

## Existing Tests

- Shared UI: `ResourceCardPickerTests`, `ListDetailShellTests`.
- CRM/HR components: navigation, directory/workspace freshness, privacy, secondary-page freshness, Projects integration, and Project Structure party picker tests.
- CRM/HR integration: schema, interaction, audit, and cross-module tests.
- CRM/HR Playwright: Directory, interaction, workforce, shell, privacy, and regression flows.
- Unit: CRM query load generation and agent-query tests.

## Missing Tests

- Async/server paging contract, stable ordering, stale request cancellation, explicit loader failure.
- >1,000 record page/search/tag/type behavior and bounded query proof.
- Cross-form entity-selector audit and standard-list reuse.
- Contact wizard transitions, cancel isolation, add-empty-remove regression, tags persistence/migration/import/export/merge.
- Opportunity create/detail/edit dialog state, compact filters, owner/project picker, linked project persistence.
- Currency-safe financial projection and typed unavailable states.
- Contextual CRM/HR workbench title and restore-key behavior.

## Risk Notes

- Adding methods to `CrmService`/`PartyDirectoryService` is easy but fails the boundary/testability goal.
- Adding page-local nested types or another page partial would be fake modularity.
- Extending shared UI with CRM enums would reverse dependency direction.
- A migration limited to the entity property without import/export/merge/snapshot review would create partial behavior.
- A full-list fallback in the picker would conceal query or transport errors and violate scale.
- Without CodeAnalytics, cycle assertions require explicit project-reference inspection and build proof.

## 2026-07-24 Follow-Up Re-entry

- `CrmHrDirectoryPage.razor` and `CrmHrWorkforcePage.razor` already use `PartyRecordBrowser`, whose query path performs source `Count` plus stable `Skip`/`Take`; the follow-up does not justify replacing that paging boundary.
- Both pages still render their selected-record workspaces permanently inside `ListDetailShell`. Directory is approximately 1,850 lines and Workforce approximately 1,500 lines, so extracting their full orchestration into new stateful components would increase refactoring risk.
- `PagedRecordBrowser` has no bounded results-scroll mode. The routed page currently owns vertical scrolling, while the Agents catalogue bounds only its card-results region.
- `CrmHrRouteCatalog` still exposes ambiguous workbench titles for Directory, Workforce, Recruiting, and Assignments even though route/tab identity is path-based.
- Web maps several `/api` areas but no `/api/crm-hr` area. CRM-HR commands already exist behind application services; an HTTP adapter can delegate without direct EF access or a new service layer.
- Local `Api:Authorization:Enabled` is false by explicit configuration. When authorization is enabled, the existing `/api` group requires authentication, but issued JWT scopes are descriptive claims and are not currently enforced as endpoint policies. The CRM-HR API must not claim scope isolation that the platform does not implement.
- The existing host contains no general CRM-HR demo-data startup hook. Follow-up scenarios must be created by an external, idempotent HTTP operator flow.

## Follow-Up Risks

- A default browser overflow change would create nested scrolling in picker dialogs; bounded results scrolling must be typed and opt-in.
- Route-selected dialogs need request-generation invalidation so a late load cannot reopen or overwrite a closed dialog.
- API list responses must stay bounded and must not expose confidential notes or private contact values.
- Treating external codes as idempotency keys without query-before-write would create duplicates because the existing application services do not promise HTTP idempotency.
- Broadly redesigning the existing API authorization model inside this UI/data task would enlarge scope. Record the existing authenticated-group boundary honestly and keep sensitive detail operations out of the new contract.
