# CRM / HR UI decoupling: completion record

This is the maintained completion and coverage record of the CRM / HR module's UI decoupling on
`components-decoupling`. It replaces per-panel planning for the remaining work; the four earlier
records stay the detailed history of their slices and are linked, not repeated:
[Home](crm-hr-home-ui-boundary.md), [account summary and activity](crm-hr-account-activity-ui-boundary.md),
[Financials](crm-hr-financials-ui-boundary.md) and the [Prompt Gallery precedent](prompt-gallery-ui-boundary.md).

The inventory below is a coverage tool. It is not a permanent file-count or interface-count
assertion; the guards in `CrmHrUiModuleBoundaryTests` name dependency categories and roles.

## Dependency direction

```text
CanDoItAll.Web, CanDoItAll.Modules.CrmHr (routed hosts, effect adapters, services, EF model)
    -> CanDoItAll.CrmHr.UI (renderers, view contracts, presentation helpers)
        -> CanDoItAll.Modules.CrmHr.Contracts   (enums, DTOs, editor inputs, read ports, route catalog)
        -> CanDoItAll.Modules.Projects.Contracts (project admission, assignment and record-query contracts)
        -> CanDoItAll.AgentFramework.Models      (technical agent and recruiting evidence models)
        -> CanDoItAll.AppComponents.RecordBrowsing, CanDoItAll.AgentFramework.UI (shared UI families)
        -> CanDoItAll.Components.BaseLib / Common / Charts / Gantt (live sibling source)

CanDoItAll.CrmHr.UiSandbox -> CanDoItAll.CrmHr.UI (same renderers, deterministic scenario views, no module)
```

Decisions and their reasons:

- **CRM / HR contracts assembly** (`src/Modules/CanDoItAll.Modules.CrmHr.Contracts`, namespace
  `CanDoItAll.Modules.CrmHr` kept). The renderers took the module's DTOs, editor inputs and enums
  as parameters, and those types were declared next to EF entities and services, so every renderer
  dragged the implementation assembly. The types moved verbatim (no renaming, no duplicate DTO
  family): the enums out of the entity files, the workspace, recruiting, staffing, agent-directory
  and query contracts out of the service files, the five read ports
  (`IPartyRecordQueryService`, `IWorkforceRecordQueryService`, `IOpportunityPipelineQueryService`,
  `IAiAgentDirectoryQueryService`, `IPartyOrganizationAffiliationService` plus the new read-only
  `IPartyOrganizationAffiliationReader`), `WorkforceRecordClassificationPolicy`,
  `RecruitmentConversionPolicy` and `CrmHrRouteCatalog`. Entities, services, EF configuration,
  agent-context builders and bridges stayed. The assembly references SharedKernel, the Projects
  contracts and AgentFramework.Models only.
- **Projects contracts slice** (`src/Modules/CanDoItAll.Modules.Projects.Contracts`, namespace
  kept). The assignment, staffing and conversion renderers are parameterised on Projects-owned
  contracts (`ProjectWriteAdmission`, `ProjectAssignmentReference`, `ProjectAssignmentAdmission`,
  `ProjectPartyAssignmentDetail`, `ProjectPartyAssignmentUpsertRequest`, `ProjectNodeDetails`,
  `ProjectNodeReference`, the role, party-type, rate-unit and status enums, and the project record
  query contracts with `IProjectRecordQueryService`). Exactly that closure moved; the Projects
  module references it and nothing else in Projects changed. This is the narrow cross-module
  change the CRM / HR dependency required; no Projects behavior moved.
- **Paged record family** (`src/UI/CanDoItAll.AppComponents.RecordBrowsing`, namespace
  `CanDoItAll.AppComponents` kept). `PagedRecordBrowser`, `PagedRecordPickerDialog`, their
  contracts and scoped CSS needed only BaseLib and logging, while AppComponents also brings
  CanvasLib, the FileTools components, Conversations.Components and SharedKernel. CRM / HR is the
  family's only consumer today. The four files moved without change; AppComponents references the
  new project, so every existing consumer of AppComponents still sees the family. Nothing was
  duplicated.
- **Workspace surfaces and view contracts.** Each routed page keeps what a host legitimately owns
  (route and query parameters, service injection, reads, mutations, navigation, notifications,
  agent-context publication, effect lifetimes). Its markup moved to a workspace surface in the
  library. The surface binds to one typed view contract (`ICrmHr…WorkspaceView`) that the page
  implements explicitly in a `…Page.View.cs` partial: state and drafts as properties, effects as
  methods. The surface injects nothing. Host methods used as event handlers keep the host as the
  Blazor event receiver; events handled inside the surface (bindings into a host draft, lambdas)
  end with a host render through `ICrmHrWorkspaceView.RequestRender`, so render semantics equal
  the former single-component page. Static presentation helpers and nested view types the markup
  used moved to `CrmHr…WorkspacePresentation` in the library.
- **Host-owned slots.** Regions whose effect owner stays in the module are named slots of the
  surfaces, not placeholders for business rendering: `SecondaryNavigation` (area tabs navigate),
  `AccountSummary`, `Financials`, `ActivityTimeline` (the adapters and sessions of the earlier
  slices) and `AgentEvidence` / `RecruitingEvidence` (the technical owner's evidence host). Every
  slot's renderer is itself a library component, so the sandbox fills the same slots with the
  same renderers and deterministic views.
- **Read ports.** Catalog browsers and pickers inject narrow read ports declared by the contract
  assemblies; they never inject a concrete service, a command surface or `NavigationManager`
  (guarded). `PartyAffiliationPicker` moved from the affiliation command service to the new
  read-only reader port. Mutations reach the owner only through a host: `PartyImportExportActions`
  takes export, preview and apply delegates; `OpportunityConversionDialog` takes the database
  profile identifier instead of injecting the Projects admission service.
- **Accepted UI-only transitive cost.** `CanDoItAll.AgentFramework.UI` (agent cards) brings
  AgentFramework.Usage, ProviderHistory.Abstractions, Conversations.Components and Charts;
  `CanDoItAll.AgentFramework.Models` brings SharedKernel, Infrastructure.Abstractions,
  Memory.Abstractions and Capabilities.Abstractions. None reaches EF, Infrastructure, a module
  implementation, Web or Composition (guarded over the transitive closure).

## Completion map

Status values: **validated** (implemented and covered by the named lanes on the recorded
source), **implemented** (compiles and renders in production hosts; lane evidence pending),
**retained** (intentionally stays in the module, with its role).

### Shared renderers and pickers

| Responsibility | Renderer (library) | Effect owner | Status |
|---|---|---|---|
| Area tabs | `Shared/CrmHrAreaTabs` | `CrmHrSecondaryTabs` adapter navigates | implemented |
| Filter bar, paged card grid, sensitive-data callout | `Shared/CompactFilterBar`, `PagedCardGrid`, `SensitiveDataCallout` | none | implemented |
| Party catalog and picker | `Pickers/PartyRecordBrowser`, `PartyRecordPickerDialog`, `PartyPicker`, `PartyRecordPresentation` | read port `IPartyRecordQueryService` | implemented |
| Workforce catalog | `Pickers/WorkforceRecordBrowser`, `WorkforceRecordPresentation` | read port `IWorkforceRecordQueryService` | implemented |
| Affiliation picker | `Pickers/PartyAffiliationPicker` | read port `IPartyOrganizationAffiliationReader` | implemented |
| Project pickers | `Pickers/ProjectRecordPickerDialog`, `ProjectPicker`, `ProjectMultiPicker`, `ProjectRecordPickerSelection` | read port `IProjectRecordQueryService` | implemented |

### Seven areas

| Area / route | Renderers (library) | Host and adapters (module) | Status |
|---|---|---|---|
| Home `/crm-hr` | `Home/CrmHrHomeSurface` | `CrmHrHomePage`, `CrmHrHomeReadSession`, mapper | validated (earlier slice) |
| Directory `/crm-hr/directory` | `Parties/CrmHrDirectoryWorkspaceSurface`, `PartyContactMethodsEditor`, `PartyAddressesEditor`, `PartyAffiliationsEditor`, `PartyRelationshipsEditor`, `PartyMergeDialog`, `PartyImportExportActions`, `PartyEditorViewModel` | `CrmHrDirectoryPage` (+ `.View.cs`), `CrmHrActivityHistorySession`, `InteractionTimeline` adapter | implemented |
| CRM `/crm-hr/crm` | `Crm/CrmHrCrmWorkspaceSurface`, `NextActionEditor`, `OpportunityPipeline`, `OpportunityBoard`, `OpportunityEditor`, create / edit / detail / conversion dialogs, `OpportunityEditorDrafts`; `Accounts`, `Activity`, `Financials` surfaces | `CrmHrCrmPage` (+ `.View.cs`), `AccountSummaryPanel`, `InteractionTimeline`, `CrmFinancialsPanel`, `CrmAgentChatContextProvider` | implemented |
| Workforce `/crm-hr/workforce` | `Workforce/CrmHrWorkforceWorkspaceSurface`, `SkillMatrixPanel`, `CapacityTimelinePanel` (real Gantt) | `CrmHrWorkforcePage` (+ `.View.cs`), history session | implemented |
| Recruiting `/crm-hr/recruiting` | `Recruiting/CrmHrRecruitingWorkspaceSurface`, `CandidatePipeline`, `InterviewSchedulePanel`, `OnboardingChecklistPanel`, `AgentRecruitingEvidenceSurface` | `CrmHrRecruitingPage` (+ `.View.cs`), `AgentRecruitingEvidencePanel` host | implemented |
| Agents `/crm-hr/agents` | `Agents/CrmHrAgentsWorkspaceSurface`, `CrmHrAgentRecordDialog` | `CrmHrAgentsPage` (+ `.View.cs`), evidence host in the dialog slot | implemented |
| Assignments `/crm-hr/assignments` | `Assignments/CrmHrAssignmentsWorkspaceSurface`, `AssignmentsProjectContextBar`, `ProjectPartyAssignmentPanel`, `StaffingRequestEditor`, `ProjectAllocationEditor`, record card, details dialog, `ProjectAssignmentResourceGanttPanel` with its projection adapter | `CrmHrAssignmentsPage` (+ `.View.cs`) with `ProjectWriteAdmission` capture | implemented |

### Razor files retained in the module

| File | Role |
|---|---|
| `Pages/CrmHr…Page.razor` (seven) | Routed hosts: route and query identity, injected application services, reads, mutations, navigation, notifications, agent-context publication, effect lifetimes; they compose their workspace surface and its host-owned slots. |
| `Components/AccountSummaryPanel.razor` | Adapter: maps the workspace model and rejects an action whose rendered record was replaced. |
| `Components/InteractionTimeline.razor` | Adapter: forwards only admissible page requests of the history session. |
| `Components/CrmFinancialsPanel.razor` | Host of `CrmFinancialsReadSession` for one account. |
| `Components/AgentRecruitingEvidencePanel.razor` | Host of the technical owner's evidence reads and interview / attempt commands; renders `AgentRecruitingEvidenceSurface`. |
| `Components/CrmAgentChatContextProvider.razor` | Effect host without markup: publishes the CRM agent-chat context and its navigation fence. |
| `Components/CrmHrSecondaryTabs.razor` | Adapter: performs the area navigation for `CrmHrAreaTabs`. |

### Dead code removed

| Removed | Reference evidence |
|---|---|
| `AiAgentProfileEditor.razor`, `AiCapabilityList.razor` | No markup or test reference in `src` or `tests`; the only hits for the names are the unrelated `AiAgentProfileEditorModel` DTO and the editor's own use of the list. Technical agent editing lives in AgentFramework. |
| `ParticipantSyncBanner.razor` | No reference in `src` or `tests`. |
| `PartyPicker` quick-create block (`AllowQuickCreate`, `QuickCreate`, `CreateParty`) | No consumer sets any of the three parameters; the block was the picker's only use of Projects quick-create types. The never-populated email and phone lines of its summary card went with it (the resolved option always carried empty strings). |

## Defects reproduced and fixed

| Defect | Reproduction | Fix | Regression |
|---|---|---|---|
| C1: Czech month labels on a Czech server | `PeriodLabel` used the ambient culture | `CrmHrPresentationCulture` | see the Financials record |
| C2: browser oracle ignored all circuit traffic and aborts | `CrmHrFinancialsBrowserTests` | `CrmHrBrowserOracle` | see the Financials record |
| Assignments search boxes received their field names | `SearchText="relationshipAssignmentSearchText"` (and three more) passed a literal to a `string` parameter, so each search input rendered the identifier as its value | the four attributes are expressions (`@…`) | pending: component fact on the rendered input values |

## Checkpoint record

- Start of this assignment: `da50a0407341990ef5be8ad8df7f471fd63811a1`, clean tree; SDK 10.0.303;
  siblings Components `7b618cda`, FileTools `7c7453c`, SharedInfo `032149c`.
- Signed checkpoints: `319965c1d` (C1 and C2).
- Structural checkpoint (contracts assemblies, record-browsing project, renderers, workspace
  surfaces, view contracts, boundary guards), Release, `/m:1`, discovery stated before execution:
  product solution, the four test solutions and the sandbox build; Unit
  `FullyQualifiedName~CrmHr|…ProjectAssignmentGanttProjectionAdapterTests|…CrmAgent|…HrAgent`
  154 / 154 plus `CrmHrEditorSnapshotTests|CrmHrMutationGateTests` 31 / 31; Components
  `FullyQualifiedName~CanDoItAll.Tests.Components.CrmHr.` 213 / 213 (`ComponentTestHarness`,
  PostgreSQL for the page facts); downstream Project Structure and assignment families 53 / 53;
  Integration CRM / HR, staffing, opportunity, party, recruitment, workforce, record-query,
  planning and HR disclosure families 152 / 152 (15 m 20 s); the non-quarantined CRM / HR browser
  lanes 7 / 7 on the real Web host and PostgreSQL. Eight browser classes that carry the
  repository's `Quarantined` trait since 2026-08 still fail on selectors the product replaced
  before this work (for example a `<select>` that has been a picker button since July); they are
  unchanged debt and are superseded by the acceptance journeys of this record, not hidden.
- Static gates at the checkpoint: portability self-tests 6 + 4; the complete-tree scan differs
  from the baseline only by ten moved paths (eight `case-policy`, two `process-start` findings of
  the moved renderers, route catalog and record browser); the baseline was refreshed for those
  moves, its diff inspected (56 lines replaced, count unchanged at 14684) and the final
  enforcement without `--write-baseline` passes. `Test-Documentation.ps1` passes for 226 files.
- Next: sandbox scenarios for the workspace surfaces, host invariant review and repairs, Project
  Structure assignment journeys, governed agent-tool evidence, browser acceptance map, closure.
