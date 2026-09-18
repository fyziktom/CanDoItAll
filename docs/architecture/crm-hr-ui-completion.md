# CRM / HR UI decoupling: completion record

This is the maintained completion and coverage record of the CRM / HR module's UI decoupling on
`components-decoupling`. It replaces per-panel planning for the remaining work; the four earlier
records stay the detailed history of their slices and are linked, not repeated:
[Home](crm-hr-home-ui-boundary.md), [account summary and activity](crm-hr-account-activity-ui-boundary.md),
[Financials](crm-hr-financials-ui-boundary.md) and the [Prompt Gallery precedent](prompt-gallery-ui-boundary.md).

The inventory below is a coverage tool. It is not a permanent file-count or interface-count
assertion; the guards in `CrmHrUiModuleBoundaryTests` name dependency categories and roles.

The rules this module proved, and the ones the earlier extractions proved, are written down once in
[UI component seams](ui-component-seams.md); this record is what CRM / HR actually did with them.

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
source, see [Evidence](#evidence)), **implemented** (compiles and renders in production hosts; lane
evidence pending), **retained** (intentionally stays in the module, with its role).

### Shared renderers and pickers

| Responsibility | Renderer (library) | Effect owner | Status |
|---|---|---|---|
| Area tabs | `Shared/CrmHrAreaTabs` | `CrmHrSecondaryTabs` adapter navigates | validated |
| Filter bar, paged card grid, sensitive-data callout | `Shared/CompactFilterBar`, `PagedCardGrid`, `SensitiveDataCallout` | none | validated |
| Party catalog and picker | `Pickers/PartyRecordBrowser`, `PartyRecordPickerDialog`, `PartyPicker`, `PartyRecordPresentation` | read port `IPartyRecordQueryService` | validated |
| Workforce catalog | `Pickers/WorkforceRecordBrowser`, `WorkforceRecordPresentation` | read port `IWorkforceRecordQueryService` | validated |
| Affiliation picker | `Pickers/PartyAffiliationPicker` | read port `IPartyOrganizationAffiliationReader` | validated |
| Project pickers | `Pickers/ProjectRecordPickerDialog`, `ProjectPicker`, `ProjectMultiPicker`, `ProjectRecordPickerSelection` | read port `IProjectRecordQueryService` | validated |

### Seven areas

| Area / route | Renderers (library) | Host and adapters (module) | Status |
|---|---|---|---|
| Home `/crm-hr` | `Home/CrmHrHomeSurface` | `CrmHrHomePage`, `CrmHrHomeReadSession`, mapper | validated (earlier slice) |
| Directory `/crm-hr/directory` | `Parties/CrmHrDirectoryWorkspaceSurface`, `PartyContactMethodsEditor`, `PartyAddressesEditor`, `PartyAffiliationsEditor`, `PartyRelationshipsEditor`, `PartyMergeDialog`, `PartyImportExportActions`, `PartyEditorViewModel` | `CrmHrDirectoryPage` (+ `.View.cs`), `CrmHrActivityHistorySession`, `InteractionTimeline` adapter | validated |
| CRM `/crm-hr/crm` | `Crm/CrmHrCrmWorkspaceSurface`, `NextActionEditor`, `OpportunityPipeline`, `OpportunityBoard`, `OpportunityEditor`, create / edit / detail / conversion dialogs, `OpportunityEditorDrafts`; `Accounts`, `Activity`, `Financials` surfaces | `CrmHrCrmPage` (+ `.View.cs`), `AccountSummaryPanel`, `InteractionTimeline`, `CrmFinancialsPanel`, `CrmAgentChatContextProvider` | validated |
| Workforce `/crm-hr/workforce` | `Workforce/CrmHrWorkforceWorkspaceSurface`, `SkillMatrixPanel`, `CapacityTimelinePanel` (real Gantt) | `CrmHrWorkforcePage` (+ `.View.cs`), history session | validated |
| Recruiting `/crm-hr/recruiting` | `Recruiting/CrmHrRecruitingWorkspaceSurface`, `CandidatePipeline`, `InterviewSchedulePanel`, `OnboardingChecklistPanel`, `AgentRecruitingEvidenceSurface` | `CrmHrRecruitingPage` (+ `.View.cs`), `AgentRecruitingEvidencePanel` host | validated |
| Agents `/crm-hr/agents` | `Agents/CrmHrAgentsWorkspaceSurface`, `CrmHrAgentRecordDialog` | `CrmHrAgentsPage` (+ `.View.cs`), evidence host in the dialog slot | validated |
| Assignments `/crm-hr/assignments` | `Assignments/CrmHrAssignmentsWorkspaceSurface`, `AssignmentsProjectContextBar`, `ProjectPartyAssignmentPanel`, `StaffingRequestEditor`, `ProjectAllocationEditor`, record card, details dialog, `ProjectAssignmentResourceGanttPanel` with its projection adapter | `CrmHrAssignmentsPage` (+ `.View.cs`) with `ProjectWriteAdmission` capture | validated |

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

Generated interface words are English (C1); amounts keep the host's number format, which C1
deliberately left unchanged (see the Financials record).

| Defect | Reproduction | Fix | Regression |
|---|---|---|---|
| C1: Czech month labels on a Czech server | `PeriodLabel` used the ambient culture | `CrmHrPresentationCulture` | see the Financials record |
| C2: browser oracle ignored all circuit traffic and aborts | `CrmHrFinancialsBrowserTests` | `CrmHrBrowserOracle` | see the Financials record |
| Assignments search boxes received their field names | `SearchText="relationshipAssignmentSearchText"` (and three more) passed a literal to a `string` parameter, so each search input rendered the identifier as its value | the four attributes are expressions (`@…`) | `CrmHrWorkspaceSandboxTests.Assignments_search_inputs_never_render_the_literal_property_names`, `CrmHrAssignmentsJourneyTests` |
| A second Save while a new party was being written created two parties | `CrmHrDirectoryPageFreshnessTests.A_second_save_dispatched_while_a_new_party_is_being_written_creates_it_once` failed with two parties without the gate | `CrmHrMutationGate` and snapshot submissions in the five editing hosts | the same fact; `CrmHrAssignmentsMutationTests` (double dispatch, typing after dispatch) |
| Saving one editor, selecting an opportunity or an agent refresh discarded unsaved drafts of the other editors of the same record | `CrmHrSameTargetRerenderTests` (three facts) failed on `Assert.Same` for CRM, Workforce and Recruiting with the preservation disabled | `CrmHrDraftReconciler` in the three hosts | the same facts; `CrmHrDraftReconcilerTests` (field merge, owner changes kept) |
| A retired Assignments save reset the draft of the project selected since | `CrmHrAssignmentsMutationTests.A_late_save_of_a_retired_project_never_resets_the_draft_of_the_project_selected_since` | reset only the submitted draft instance of the current selection | the same fact |
| A failed read-back after a committed write escaped as a failure (and invited a repeated write) | host review of the Recruiting, Workforce, Directory and CRM refresh paths | refresh failure is a warning; no rethrow, no replay | no dedicated automated fact: these hosts read through concrete services without a seam; recorded as a remaining limitation |
| Dialog footer Save bypassed form validation (a parse error saved the previous value) | a footer button outside the form called the host directly | `CrmHrWorkspaceSurface.SubmitAsync` validates the form's `EditContext` | `CrmHrFooterSaveValidationTests` (Workforce capacity, Directory role date) |
| Removing a Directory role or confidential note failed the circuit | the Remove lambda read the shared loop variable after the loop (row 0 rendered test ids ending in `-1`); the host's `RemoveAt(Count)` threw | per-row copies; rows removed by instance | `CrmHrDirectoryRowActionTests` (two facts), `CrmHrDirectoryRelationshipsPrivacyJourneyTests` |
| Evidence commands could re-enter and complete into another candidate | `AgentRecruitingEvidencePanel` review | re-entry guard, identity fence, write failure separated from read-back | `AgentRecruitingEvidencePanelTests` |
| The opportunity detail opened beneath the account dialog from a board card or the deep link | agent journey probe at `4a5724977`: the hit test at the detail dialog's center returned the account dialog (evidence `output/agent-worktree-evidence/ab98d579660c62c93/playwright/crm-hr-opportunity-defects/detail-beneath-account-dialog`) | owned overlays render inside the account dialog; the shared dialog re-raises nested open dialogs | `CrmHrOpportunityJourneyTests` (card and deep link, run three times after the fix) |
| Confirming a conversion that relinks the linked project failed the circuit | agent journey at `4a5724977`: `InvalidOperationException` from `ProjectAssignmentAdmission.Require` (host log in the same evidence folder) | the load captures the linked project's admission; missing or stale admissions are visible rejections | `CrmHrOpportunityConversionHostTests` (three facts), `CrmHrOpportunityJourneyTests` |
| The Agents deep link to an unknown agent failed the circuit | journey console error `ObjectDisposedException` of `DotNetObjectReference<Dialog>` in `DialogInterop.OpenAsync`: the loading dialog closed while its module loaded | live-sibling `Dialog` fix (see above) | `DialogModuleLoadRaceTests` (three facts failing on the previous source), `CrmHrRouteChromeJourneyTests`, `CrmHrAgentProjectionJourneyTests` |
| A reopened BaseLib dialog never opened again after a close during its module load | `DialogModuleLoadRaceTests.A_dialog_closed_while_its_module_loads_opens_nothing_until_it_is_opened_again` timed out on the previous source | same fix | same fact |

## State and effect ownership

Each routed host owns one workspace or editor lifetime per selected record; the renderers own none.
The repairs below restore the shared invariants where the hosts did not meet them. Each has a
reproduction or a failing regression recorded in the defects table.

- **One admitted write per editor lifetime.** `CrmHrMutationGate` (module) admits one mutation per
  host lifetime key (CRM account load stamp, Workforce and Recruiting action generation, Assignments
  project selection lifetime, Directory editor). A second dispatch while a write is in flight is
  dropped; a retired lifetime never blocks or releases its successor.
- **Independent submissions.** Every editor input has a `Snapshot()` (memberwise clone with
  independent lists and nested inputs); hosts submit the snapshot captured before the first await,
  so typing after dispatch never reaches the owner. `CrmHrEditorSnapshotTests` checks every public
  settable member of every snapshot, nested collections included.
- **Committed, then refresh.** A failed read-back after a committed write is a warning ("Saved,
  refresh failed"), never a rethrow, a replay or a reset of the committed identity.
- **Drafts of the same record survive other reloads.** `CrmHrDraftReconciler` decides what a
  read-back does to each editor of a host: a new target, the editor's own commit or a clean draft
  takes the owner's values; a dirty draft of the same target keeps the fields the operator touched,
  takes the owner's newer value of every untouched field and keeps its instance (and its
  `EditContext`). Applied to CRM (profile, connections, new interaction), Workforce (profile, skill,
  capacity block) and Recruiting (application, interview, lifecycle task, support roles,
  conversion). Closing a record discards its drafts.
- **Alternate actions validate the same form.** Dialog footer Save buttons of the Directory and
  Workforce editors sit outside the form element; they validate the form's `EditContext` (parsing
  errors included) before the host sees the action.
- **Row actions name their row.** Directory role and confidential-note rows remove the instance
  their button rendered, not the shared loop variable.
- **Project admissions are captured, not refreshed.** A conversion that relinks an already linked
  project writes against the admission captured by the load that showed the project; a missing or
  stale admission is a visible rejection (`ProjectWriteAdmissionRejectedException` included), never
  an unhandled circuit failure. Assignments keeps its captured selection admission and ignores a
  retired selection's completion.
- **Owned overlays.** The opportunity create, detail, edit and conversion dialogs and the connection
  picker render inside the account record dialog that owns them and close with it. The shared
  `Dialog` (live sibling, see below) keeps nested dialogs above their owner and never opens a dialog
  that was closed or removed while its module loaded.
- **Evidence host.** `AgentRecruitingEvidencePanel` admits one command at a time, fences completion
  by the loaded identity and separates a failed write from a failed read-back.

### Narrow live-sibling change

`CanDoItAll.Components` (`development`): `Dialog` and `DialogInterop` open only for the latest open
request of a dialog that is still rendered open and release a module whose dialog was disposed while
it loaded; `Dialog.razor.js` re-raises open dialogs rendered inside the opened dialog. Reason: the
CRM / HR Agents deep link to an unknown agent opened and closed its loading dialog at once and the
late open serialized a disposed `DotNetObjectReference`, failing the circuit; the opportunity deep
link opened the account dialog above its own detail dialog. Regression: `DialogModuleLoadRaceTests`
(three facts, each failing on the previous source) and the Agents and Opportunity browser journeys.
The standard source-package snapshot is refreshed for the three dialog files.

## Backend-free sandbox

`CanDoItAll.CrmHr.UiSandbox` composes every workspace surface and the recruiting evidence surface
with deterministic sandbox views that implement the same view contracts, and five read-port fakes
over synthetic data (`AddCrmHrSandboxReadPorts`). Routes `/crm-hr/workspaces/{directory|crm|
workforce|recruiting|agents|assignments}` and `/crm-hr/agents/recruiting-evidence`, each with
`?scenario=` and `&layout=matched|constrained`. Scenarios per archetype: catalog, selected existing
record, new draft, non-default section, loading, failed, unavailable references, empty, busy; skips
are recorded in the sandbox README with the view-contract reason (for example, the Agents record
dialog keeps its tab index locally). Scenario actions change only sandbox state and write an intent
line; nothing reaches a CRM database. The sandbox references the rendering library and its
dependency categories only (guarded transitively by `CrmHrHomeSandboxTests`).

## Browser acceptance map

Real Web host, PostgreSQL disposable profile per fixture, synthetic records with a per-run suffix,
1600 x 1000 plus the constrained 1100 x 900 checks named in each journey, Playwright trace and
screenshots under `output/playwright/<journey>/` (ignored), and `CrmHrBrowserOracle` on every page
(page errors, console errors, failed requests, HTTP >= 400, error UI, reconnect modal; the only
allowance is the aborted `/_blazor/disconnect` of the journey's own navigations, written to
`expected-teardown.txt`). Every write is one click after the interactive-ready signal and is read
back through its owner by exact identity.

| Required journey | Test | Observed outcome |
|---|---|---|
| Route / chrome | `CrmHrRouteChromeJourneyTests` (2) | seven routes with title, header and active tab; tab navigation and Back; deep links with reload and close; unknown account, party, application, agent and project identities open nothing |
| Directory create / edit | `CrmHrDirectoryEditJourneyTests` | rejected save without a name; create; reopen the returned id after reload; edit; draft kept across editor tabs; cancelled draft never persisted |
| Directory relationships / privacy | `CrmHrDirectoryRelationshipsPrivacyJourneyTests` | contact, address, role, tag, affiliation and relationship through the real editors and pickers; a confidential note visible only in the trusted dialog, absent from the catalog, its search and Home |
| Directory lifecycle | `CrmHrDirectoryLifecycleJourneyTests` | archive and reactivate; duplicate merged with contact and relationship moved; CSV export, preview, one applied row, one blocked duplicate, nothing deleted |
| CRM workspace | `CrmHrCrmWorkspaceJourneyTests` | profile edit and active-customer conversion; connected records via the picker; interaction with participant and overdue follow-up; a second account untouched |
| Opportunities | `CrmHrOpportunityJourneyTests` | create (three steps, picker), edit, advance with stage history, rejected edit, conversion into exactly one project, relink of the preselected project, detail above the account dialog from a board card and from the deep link |
| History / Financials | `CrmHrAccountActivityBrowserTests`, `CrmHrFinancialsBrowserTests` (earlier slices, rerun) | unknown versus accepted empty, paging, English month labels on the Czech host, seeded plot values and geometry |
| Workforce | `CrmHrWorkforceJourneyTests` | profile create and edit, skill definition and party skill, leave block added and deleted next to the plotted schedule, independent history, reloaded deep link |
| Recruiting | `CrmHrRecruitingJourneyTests` | cancelled draft leaves nothing; application for a picked candidate; interview, support roles, lifecycle task; approval and conversion to workforce on the same party |
| Agent projection | `CrmHrAgentProjectionJourneyTests` | managed defaults synchronized through the shell action; read-only record with technical identity; technical editor navigation; unknown party opens nothing; human-owned party fields kept after a directory edit |
| Assignments / Project Structure | `CrmHrAssignmentsJourneyTests`, `ProjectStructureTaskAssigneeJourneyTests` | participation, staffing request, allocation create and delete, schedule, search boxes, A -> B -> A without stale rows; task assignee person and synchronized agent chosen in the real task dialog, replaced and cleared, read back in Work, Gantt, Assignments and the workforce record |
| Agent integration | `CrmHrLiveAgentToolUiSmokeTests` (opt-in, live lane below) | planner grants saved in the agent settings UI; a planning read from the Project Structure chat; HR approval UI rejected once and approved once |
| Existing regressions | `CrmHrHomeBrowserTests`, `CrmHrSensitiveDataFlowTests` | rerun unchanged |

### Retired browser scripts

Eight quarantined browser classes were removed. `CrmHrDirectoryFlowTests` and `CrmHrShellSmokeTests`
went with the Directory and route journeys that replaced them. The remaining six
(`CrmHrCrossModuleFlowTests`, `CrmInteractionFlowTests`, `OpportunityPipelineTests`,
`ProjectPartyAssignmentFlowTests`, `RecruitmentFlowTests`, `StaffingFlowTests`) were audited before
they were removed, not simply deleted:

- They executed nothing. All six were run against this candidate and all six failed, on selectors
  the product replaced (the party picker that succeeded a `<select>`, the party form that moved into
  a dialog) and on the assignment admission that now requires a captured project lifetime.
- They could never have run in CI. Each wrote its screenshots to an absolute Windows path of the
  author's own workstation, below an evidence directory that no runner has.
- They asserted almost nothing: two to eighteen assertions each, mostly screenshots.
- Their flows are covered by the journeys in the table above, and the party pickers of Project
  Structure are covered by `ProjectStructurePartyPickerTests`.

What they uniquely drove was a set of **secondary editor fields**. The opportunity's economics and
attribution, the lost reason and the note of a stage change, and the recruiting interview outcome,
recommendation and feedback, stage note and lifecycle task note are now typed into the shipped forms
and read back from their owners by `CrmHrEditorFieldCoverageTests`.

These are still not driven by any executed test and are named here rather than left implicit. Their
persistence is covered at the owners by the Unit and Integration lanes; what is missing is a
UI-level check that each input is still bound:

| Not driven through the UI | Owner coverage |
|---|---|
| Recruiting candidate contact and summary fields; recruiter, hiring manager, interviewer and target unit; support manager, buddy and mentor; conversion home unit, manager, location, seniority, start date and time zone; the application ownership tab | `RecruitingService` Unit and Integration tests |
| Staffing request requested-by and delivery unit; the allocation candidate skill filter | `HrService` staffing Integration tests |
| The opportunity board columns and the stage and partner filters | `OpportunityBoardTests` (Components) for the board; the filters have no UI-level fact |
| The catalog search boxes `crmhr-account-search` and `crmhr-recruiting-applications-search` | the paged record family's own Components tests |

## Project Structure, task assignees and project lifetime

Work owns direct task assignees (`Workbench_WorkAssignments`); CRM / HR owns parties,
affiliations, rates and participation. Browser proof: `ProjectStructureTaskAssigneeJourneyTests`.
Owner and interleaving proof at the integration layer (unchanged owners, rerun at closure):
`WorkAssignmentAdmissionIntegrationTests`, `ProjectWriteAdmissionIntegrationTests`,
`ProjectCurrentLifetimeBatchIntegrationTests`, `ProjectPartyAssignmentIntegrationTests`,
`PartyMergeIntegrationTests`, and the Components families `ProjectStructureWorkItemAssigneeServiceTests`,
`ProjectStructureTaskDetailsServiceTests`, `ProjectStructurePageTaskAssigneeCreationTests` and
`CrmHrAssignmentsMutationTests` (retired selection, double dispatch).

## Governed agent tools

- **A. Composition and policy:** `CrmPlanningToolAdmissionTests` (inactive, foreign or mistyped
  actors; project-scope read authority; interactive sessions only), `HrAgentRuntimeToolProviderTests*`,
  `HrAgentAuthorizationTests`, `HrAgentCompositionTests`, `MafAgentRuntimeToolProviderCompositionTests`,
  `CrmHrAgentQueryServiceTests`.
- **B. Deterministic governed dispatch:** `CrmPlanningRuntimeIntegrationTests` (a planning read
  reserves no capacity and writes no assignment or staffing record), `MafHrResultDisclosureIntegrationTests`
  (+ `.Mutations`: a denied CRM mutation never reaches the owner and a separately approved proposal
  writes exactly once), `CrmHrAgentQueryIntegrationTests`. Real providers, authorization, admission
  journal, dispatch, command owners and PostgreSQL; only the model transport is scripted.
- **C. Live model-backed smoke (opt-in):** `CrmHrLiveAgentToolUiSmokeTests` through the shipped UI
  and `CrmHrLiveAgentToolSmokeIntegrationTests` (planner read) through the runtime, with
  `CANDOITALL_RUN_LIVE_AGENT_VALIDATION=true` and `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE=true`, the
  seeded OpenAI profile (`gpt-5.4-mini`, Responses transport), synthetic records and a bound of ten
  model requests per execution. Evidence (identifiers, tool names, decisions, states, counts;
  secret-like tokens redacted) under `output/live-agent-smoke/<utc>/evidence.json`.

## Development loop

Measured on the same workstation, SDK 10.0.303, Release builds with `/m:1` and Debug `dotnet watch`,
siblings Components `7b618cda` (plus the uncommitted dialog change for the second run) and FileTools
`7c7453c`; one sample per step, so differences of a few seconds are within noise. Before: HEAD
`319965c1d`, the Directory markup in `Pages/CrmHrDirectoryPage.razor` (module). After: the working
tree of this record, the same markup in `Parties/CrmHrDirectoryWorkspaceSurface.razor` (library).
Every edit was restored from a backup.

| Step | Before | After |
|---|---:|---:|
| Library no-op build | 1.9 s | 3.5 s |
| Module no-op build | 7.5 s | 8.0 s |
| Library build after the Razor edit | 2.0 s (edit not in the library) | 7.0 s |
| Module build after the Razor edit | 10.6 s | 8.8 s |
| Sandbox (Parity) no-op / after the edit | 2.2 s / 2.2 s | 7.2 s / 7.2 s |
| Web no-op / after the Razor edit / after a scoped CSS edit | 19.5 s / 21.2 s / 19.9 s | 19.0 s / 22.4 s / 21.2 s |
| Tailwind Parity / Fast | 3.4 s / 1.9 s | 3.2 s / 2.3 s |
| Web `dotnet watch`: projects loaded | 129 in 8.9 s | 132 in 9.3 s |
| Web `dotnet watch`: startup to first 200 | 104.5 s | 100.5 s |
| Web `dotnet watch`: Razor edit to served HTML (hot reload) | 16.9 s | 8.7 s |
| Sandbox `dotnet watch`: projects, startup, Razor edit to served HTML (hot reload) | not available for this markup | 20 in 2.0 s, 25.6 s, 8.1 s |

What the numbers show: the library and the sandbox now compile the complete workspace renderer
tree with Charts and Gantt, so their own builds are several seconds slower; the Web host's builds
are unchanged within noise; both watch loops applied the Razor edit through hot reload without a
restart. The new capability is the backend-free loop: the sandbox watches 20 projects instead of
132 and serves the edited Directory surface within 26 s of startup, without PostgreSQL or the
production composition. No faster build is claimed. Asset modes: the sandbox builds in `Parity`
(shared Tailwind output) and `Fast` (the CRM / HR scoped `crmhr-fast.css`) at the closure below.

## Evidence

`CRMHR-MODULE-CLOSURE`, one source state (the working tree of this record), Release, `/m:1`,
discovery stated before every run, SDK 10.0.303, siblings Components `7b618cda` plus the dialog
change committed in that repository and FileTools `7c7453c`.

| Lane | Command / filter | Discovered | Result |
|---|---|---:|---|
| Production builds | `CanDoItAll.slnx`; the sandbox with `-p:CrmHrAssetMode=Parity` and `=Fast`; the Stable and Playwright test solutions | 5 builds | all exit 0, no errors; the Stable solution's one warning is outside CRM / HR |
| Pure and component tests | Unit `~CrmHr\|~CrmAgent\|~HrAgent\|~CrmPlanning\|~ProjectAssignmentGanttProjectionAdapterTests\|~MafAgentRuntimeToolProviderCompositionTests` | 310 | 310 passed (4 s) |
| | Components `~CanDoItAll.Tests.Components.CrmHr.\|~OpportunityBoardTests\|~AssignmentEditorAdmissionTests` (bUnit over the real hosts and PostgreSQL) | 287 | 287 passed (3 m 41 s) |
| | Components downstream Project Structure, Projects and assignment families | 51 | 51 passed (2 m 15 s) |
| PostgreSQL owner / integration and HTTP | Integration CRM / HR, interaction, financial, planning, party command, staffing, opportunity, merge, directory, affiliation, recruitment, workforce, project-party assignment, record query, agent directory, HR disclosure, work-assignment admission, project write admission and project lifetime families (includes `CrmHrApiIntegrationTests` over the real HTTP host) | 174 | 174 passed (9 m 8 s); one of them is the opt-in live planner smoke, which returns without effect here |
| Production Playwright | `(~CrmHr\|~ProjectStructureTaskAssigneeJourneyTests)&Category!=Quarantined&Category!=LiveAgent` on the real Web host and PostgreSQL | 19 | 19 passed (3 m 37 s): the 12 journeys of the acceptance map plus the Home, account-activity, Financials and sensitive-data regressions; artifacts under `output/playwright/` |
| Project Structure / task assignee end to end | `ProjectStructureTaskAssigneeJourneyTests` (browser) with `WorkAssignmentAdmission…`, `ProjectWriteAdmission…`, `ProjectCurrentLifetimeBatch…`, `ProjectPartyAssignment…` and `PartyMerge…` at the owners | in the lanes above | passed |
| Deterministic governed tool dispatch | `CrmPlanningRuntimeIntegrationTests`, `MafHrResultDisclosureIntegrationTests(+.Mutations)`, `CrmHrAgentQueryIntegrationTests`, `CrmPlanningToolAdmissionTests`, `HrAgent*`, `MafAgentRuntimeToolProviderCompositionTests` | in the lanes above | passed |
| Live model-backed tools | `CrmHrLiveAgentToolUiSmokeTests` (both scenarios) with the two live variables set | 2 | 2 passed; planner read 2 model requests, HR create 6 of a bound of 10; evidence `output/live-agent-smoke/` |
| Portability / static | self-tests 6 + 4; complete-tree scan; baseline refreshed for 33 reviewed sandbox findings (14684 -> 14719); enforcement without `--write-baseline` | — | PASS |
| Documentation | `./tools/Validation/Test-Documentation.ps1` | — | see the checkpoint record |
| Broad Stable gate | `CanDoItAll.Tests.Stable.slnx` with the documented CI filter, once at this checkpoint | — | see the checkpoint record |
| Dependency and watch graph | Release builds and `dotnet watch` for the module, library, sandbox (Parity and Fast) and Web host | — | see [Development loop](#development-loop) |

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
- Third checkpoint (this record): host invariant repairs, the backend-free workspace sandbox, the
  browser acceptance journeys, the Project Structure assignee journey, governed agent-tool
  evidence and the opt-in live smoke. Lanes and results are in [Evidence](#evidence); the
  documentation validator passes for 226 maintained Markdown files.
- The CRM / HR dialog repair in the live sibling `CanDoItAll.Components` (`development`) is a
  separate signed commit in that repository; this repository builds against its working tree.
- Live lane: executed with the seeded OpenAI profile on 2026-09-17; 16 model requests in total
  across all attempts, within the per-execution bound of ten. The managed HR scenario is proven
  through the shipped UI; the same flow driven from an integration test could not publish the
  Agents page's workspace position and chat context without re-implementing shell internals, so
  that scenario was removed from `CrmHrLiveAgentToolSmokeIntegrationTests` (the planner read
  stays) and the UI smoke is the level-C evidence for managed HR mutations.
- Broad Stable gate, once at this checkpoint (`CanDoItAll.Tests.Stable.slnx` with the documented CI
  filter, Release, `--no-build`, 198 min): Unit 8837 / 8838, Components 2287 / 2288, Integration
  3014 / 3015, AgentFramework.Memory 22 / 22, Memory 203 / 203. The three failures are repository
  debt that this work neither caused nor fixed, and they stay visible:
  `WorkflowsPageTests.Workflow_canvas_preview_selects_running_node_from_progress` and
  `ProviderHistoryRuntimeIntegrationTests.Scale_capture_and_cleanup_remain_bounded_under_concurrent_search`
  are load-sensitive under the full gate, and `SecretScanningTests.Repository_contains_no_realistic_provider_keys`
  reports the retained operator drafts. Investigated with sanitized evidence: all ten reported
  findings are copies of one integration test file under
  `artifacts/modules-decoupling/drafts/**`, whose synthetic `SecretValue` fixture (24 characters,
  low character diversity, placeholder wording) matches the OpenAI pattern; the tracked current
  version of that file no longer contains the literal. No repository source and no artifact of this
  task is flagged. The drafts were not modified, nothing was quarantined and no pattern was
  weakened.
- Remaining limitations: the browser-level field gaps listed under
  [retired browser scripts](#retired-browser-scripts); the repository's retained-artifact
  secret-scanning finding, which is unrelated to this work and unchanged.

  Two admitted gaps of the first closure are now closed. The committed-with-warning read-back of the
  four editing hosts is proven by `CrmHrCommittedReadBackTests` through a controlled failure of
  exactly the owner read each host issues after its commit, and the draft a save leaves behind is
  proven by `CrmHrPostDispatchEditTests`; see
  [reconciliation after a commit](ui-component-seams.md#reconciliation-after-a-commit).
