# Scope Inventory

## Production Source Inventory

| Area | Existing source | Planned role |
| --- | --- | --- |
| Shared typed picker | `repo://src/UI/CanDoItAll.AppComponents/Components/ResourceCardPicker.razor`, `ResourceCardPickerOption.cs` | Reference/compatibility source; extract or evolve a neutral async paged browser without breaking current consumers. |
| Shared picker tests | `repo://tests/Components/CanDoItAll.Tests.Components/ResourceCardPickerTests.cs` | Extend with loader, paging, cancellation, error, and constrained-container behavior. |
| Server-paged picker precedent | `repo://src/Modules/CanDoItAll.Modules.Prompts/Components/PromptGallerySearchList.razor`, `PromptGalleryPickerDialog.razor`, `repo://src/Modules/CanDoItAll.Modules.Prompts/PromptGalleryContracts.cs`, `EfPromptGallerySearchDriver.cs` | Extract domain-neutral typed query/page/loading/pager/dialog mechanics instead of designing a second protocol. |
| CRM module composition | `repo://src/Modules/CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`, `Services/CrmHrModuleServiceCollectionExtensions.cs`, `_Imports.razor` | Add only directionally approved references/imports and register top-level query/projection services. |
| Current CRM/HR picker primitives | `Components/PartyPicker.razor`, `PagedCardGrid.razor`, `CompactFilterBar.razor` | Make `PartyPicker` a thin shared-browser host or remove it; preserve useful compact/grid composition without presenting in-memory paging as scalable. |
| Directory | `Pages/CrmHrDirectoryPage.razor`, `Components/PartyContactMethodsEditor.razor`, `Components/PartyAddressesEditor.razor`, `Components/PartyRelationshipsEditor.razor` | TagEditor adoption, scalable list/relationship picking, contact wizard host, safe draft orchestration, and closure-capture fixes. |
| Party persistence | `Models/CrmHrFoundationModels.cs`, `Services/CrmHrServices.cs`, `Services/PartyDirectoryManagementService.cs`, `Integration/CrmHrCrossModuleIntegration.cs`, `Services/CrmHrSourceSnapshotProvider.cs` | Persist and project contact tags; keep new query logic outside large partial services. |
| PostgreSQL migrations | `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj`, `Migrations/AppDbContextModelSnapshot.cs` | Add/verify backward-compatible contact-tag column migration. |
| CRM opportunities | `Pages/CrmHrCrmPage.razor`, `Components/OpportunityBoard.razor`, `Components/OpportunityEditor.razor`, `Components/OpportunityConversionDialog.razor` | Extract dialog state/presentation, compact typed filtering, reusable pipeline, party/project pickers, and Financials tab orchestration. |
| CRM models/services | `Models/CrmHrBusinessModels.cs`, `Services/CrmHrServices.cs` | Preserve write behavior; add cohesive read/query services instead of extending the monolith where possible. |
| Projects | `repo://src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs`, `Pages/Components/ProjectsBoard.razor`, `CanDoItAll.Modules.Projects.csproj` | Own project search/page adapter and presentation mapping consumed by CRM/HR. |
| Charts | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/ProviderUsageDialog.razor`, `CanDoItAll.Modules.AgentFramework.csproj` | Existing `CdaChart` bar/donut reference; add matching Charts dependency/import to CRM/HR. |
| Workbench titles | `repo://src/App/CanDoItAll.Web/Components/Layout/MainLayout.Workbench.cs`, `Composition/ShellNavigation.cs`, `repo://src/Foundation/CanDoItAll.SharedKernel/Navigation/ShellNavigationItem.cs`, `repo://src/UI/CanDoItAll.AppComponents/Components/AppTabStrip.razor` | Resolve contextual CRM/HR titles through a typed CRM route catalog; preserve main navigation and account for the tab strip's `9rem` visible-title truncation. |
| CRM navigation | `Components/CrmHrSecondaryTabs.razor`, `Agents/CrmHrAgentChatSurfaceBuilder.cs` | Centralize typed route/title metadata where practical; avoid duplicate magic route strings. |

## Existing Shared Component Choices

- Reuse BaseLib `Dialog`, `TagEditor`, `TextBox`, `Button`, `Grid`, `Stack`, `Cluster`, `Split`, `FormField`, `FormSection`, `SecondaryTabs`, `StatusBadge`, `EmptyState`, `TooltipTarget`, and existing list-detail shell wrappers.
- Reuse `CdaChart` from `CanDoItAll.Components.Charts` for bar and donut visuals; the host already loads chart assets.
- Reuse existing CRM/HR `CrmHrSecondaryTabs`, `CompactFilterBar`, `PagedCardGrid`, `PartyPicker`, `OpportunityBoard`, and `ProjectAssignmentDetailsDialog` only where their semantics fit. Do not preserve an in-memory/dropdown contract merely because the component exists.
- Reuse `ProjectsBoard` and Agent catalog presentation as visual references, not by creating an illegal module dependency.
- Components MCP was intermittent. A successful recommendation selected BaseLib `Dialog`, form wrappers, `DataGrid`, `ListDetailShell`, and typed `CdaChart`; source usage and the compact UI composition reference cover the later transport-closed setup gap.

## Selector Audit Inventory

The implementation audit must classify each CRM/HR `InputSelect`:

- Keep finite enum/status choices such as `PartyType`, lifecycle, opportunity stage/source, and relationship kind.
- Replace or explicitly justify high-cardinality entity choices currently found in:
  - `PartyRelationshipsEditor.razor` related party;
  - `CrmHrCrmPage.razor` stakeholder, related opportunity, opportunity owner/delivery/partner filters;
  - `NextActionEditor.razor` owner;
  - `OpportunityEditor.razor` owner, delivery unit, and linked party;
  - `AiAgentProfileEditor.razor`;
  - `CrmHrWorkforcePage.razor`;
  - `CandidatePipeline.razor`, `InterviewSchedulePanel.razor`, `OnboardingChecklistPanel.razor`, and `CrmHrRecruitingPage.razor`;
  - `StaffingRequestEditor.razor`;
  - `PartyPicker.razor` consumers in project allocation/assignment;
  - project dropdowns in `OpportunityConversionDialog.razor`.
- Record every retained entity dropdown with a bounded-cardinality justification. An undocumented retention fails SB02/SB04.

## Test Inventory

| Test project | Existing anchors | Planned additions |
| --- | --- | --- |
| Components | `ResourceCardPickerTests`, `PromptGallerySearchListTests`, `PromptGalleryPickerDialogTests`, `CrmHrDirectoryPageFreshnessTests`, `CrmHrWorkspaceFreshnessTests`, `CrmHrNavigationTests`, `ListDetailShellTests`, `ProjectsCrmHrIntegrationTests`, `ProjectStructurePartyPickerTests`, `PartyRelationshipsEditorTests` | Shared browser contract, contact/address/relationship callback identity, contact wizard, tag editor, opportunity dialogs/pipeline, project picker, Financials rendering, contextual route-catalog tests. |
| Unit | `CrmHrAgentQueryServiceTests`, `CrmQueryLoadGenerationTests`, `WorkbenchStateServiceTests` | Query validation/stable paging, financial projection/availability, dialog/load-generation state, tab descriptor identity. |
| Integration | `CrmHrSchemaIntegrationTests`, `CrmInteractionIntegrationTests`, `CrmHrCrossModuleIntegrationTests`, `CrmHrAuditTrailIntegrationTests` | Contact tag migration/round-trip, >1,000-record bounded query, opportunity/project link, sold aggregation. |
| Playwright | `CrmHrDirectoryFlowTests`, `CrmInteractionFlowTests`, `CrmHrRegressionTests`, `CrmHrShellSmokeTests`, `CrmHrWorkforceFlowTests` | All required dialogs, compact filters, charts, and multiple contextual workbench tabs at large desktop. |

## Browser And Evidence Inventory

- Preferred viewport: `1800x1100`; minimum fallback `1600x900`.
- Routes: `/crm-hr/assignments`, `/crm-hr/directory`, `/crm-hr/crm`, `/crm-hr/workforce`, `/crm-hr/recruiting`, `/crm-hr/agents`.
- Evidence root: `bundle://evidence/browser/SB01/` through `bundle://evidence/browser/SB06/`.
- Every overlay capture records focus target, layering/clipping, body scroll, footer action visibility, loading/error/empty state, and lateral overflow.
- Primary-surface captures record first-viewport usefulness and the single actual scroll owner.

## Explicitly Out Of Scope

- Source artifacts under `inputs/` are immutable.
- No Radzen packages/components.
- No responsive application-page expansion.
- No invoice/purchase schema or fake financial fixtures presented as production behavior.
- No broad service/page rewrite unrelated to `N002`-`N010`.
