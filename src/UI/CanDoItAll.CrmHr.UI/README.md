# CRM / HR rendering UI

This Razor class library owns the rendering of the CRM / HR module: the seven workspace surfaces,
the editors, panels, pickers and dialogs they compose, and the four presentation-record surfaces
of the earlier slices. The production hosts in `CanDoItAll.Modules.CrmHr` and the scenario
sandbox render the same components, so there is no second renderer for any of them.

The rules behind this layout, and when the other seam shape is the right one, are in
[UI component seams](../../../docs/architecture/ui-component-seams.md).

## Layout

- `Home/`, `Accounts/`, `Activity/`, `Financials/`: controlled surfaces over immutable
  presentation records with typed intents (`CrmHrHomeSurface`, `CrmHrAccountSummarySurface`,
  `CrmHrActivitySurface`, `CrmHrFinancialsSurface` with the real `CdaChart`). Their parameters
  expose only this library's own records and UI primitives.
- `Parties/`, `Crm/`, `Workforce/`, `Recruiting/`, `Agents/`, `Assignments/`: one workspace
  surface per routed area (`CrmHr…WorkspaceSurface`) with its view contract
  (`ICrmHr…WorkspaceView`), its presentation helpers (`CrmHr…WorkspacePresentation`) and the
  area's editors, panels and dialogs. `Recruiting/` also holds the agent assessment evidence
  surface and its view contract.
- `Pickers/`: the party, workforce, affiliation and project catalog browsers and pickers over
  the shared paged record family.
- `Shared/`: the area tabs renderer, filter bar, paged card grid and sensitive-data callout.
- `CrmHrWorkspaceSurface.cs`: the base of the workspace surfaces. `CrmHrPresentationCulture.cs`:
  the presentation-language policy.

## Workspace surfaces and view contracts

A workspace surface renders one view contract that its host implements: state and drafts as
properties, effects as methods. The surface injects nothing and owns no business state. A host
method used as an event handler keeps the host as the Blazor event receiver; an event handled
inside the surface (a binding into a host draft, a lambda that calls a host method) ends with a
host render through `ICrmHrWorkspaceView.RequestRender`, so rendering behaves as when the markup
lived in the host. Regions whose effect owner stays with the host are named slots:
`SecondaryNavigation` (the area tabs navigate), `AccountSummary`, `Financials`, `ActivityTimeline`
and the agent evidence regions. Every slot renderer is a component of this library, so a
backend-free host fills the same slots with the same renderers.

Catalog browsers and pickers inject narrow read ports declared by the contract assemblies
(`IPartyRecordQueryService`, `IWorkforceRecordQueryService`, `IOpportunityPipelineQueryService`,
`IPartyOrganizationAffiliationReader`, `IProjectRecordQueryService`). No component injects a
concrete service, a command surface or `NavigationManager`, and no component writes: mutations
are host methods or host-supplied delegates (`PartyImportExportActions`), and a project admission
is bound to the profile identifier the host passes (`OpportunityConversionDialog`).

Directory relationship forms use typed value, input and pending-input callbacks. Their
reconciliation keeps the original membership/row versions, raw notes, native date input
and stable row identities separate from accepted data. `RelationshipEditing` renders
the comparison and local Apply action; the module controller owns authorized reads,
conditional writes, receipt handling and observation disposal. Large collections use
complete snapshot choices with bounded previews, never partial replacement payloads.

Navigation state is host state like any other. A routed host passes the committed catalog query
(`CatalogQuery`) and the selected record section through its view contract and receives the
catalog's committed changes (`HandleCatalogQueryChangedAsync`) and section changes as intents; it
keeps both in the page URL. The surfaces never read or build a URL. A backend-free host passes no
catalog query, and the catalog then keeps its own.

Three rules keep the surfaces' actions on the record the operator sees. An action outside its form
element (a dialog footer Save) validates the form's `EditContext`, parsing errors included, through
`CrmHrWorkspaceSurface.SubmitAsync` before the host sees it. A row action inside a loop names the
row instance it rendered (or a per-row copy of the index), never the shared loop variable, because
component child content runs after the loop. An overlay that belongs to a record dialog (the
opportunity and connection dialogs of the CRM account) renders inside that dialog, so it closes
with it and stacks above it.

Presentation language: interface words a formatter generates are English under every server
culture. `CrmHrPresentationCulture` formats month labels (`Jan 2025`) and forces English AM/PM
designators into the ambient short timestamp; numbers, separators, the date order and the time
zone stay with the ambient culture, machine formats are explicit where they are produced, and
nothing parses input or changes the process culture.

Surfaces of the earlier slices carry an `IsInteractive` input where a host renders them before
its circuit can handle events, every account action captures the record it was rendered for, and
the timeline renders what the host accepted for its target, so counts that were not accepted are
shown as unavailable rather than as zero.

## Dependencies

The authoritative list is in [CanDoItAll.CrmHr.UI.csproj](CanDoItAll.CrmHr.UI.csproj):

- UI primitives: `CanDoItAll.Components.BaseLib` (with `Common`), `Charts` (which brings
  Blazor-ApexCharts; never referenced directly) and `Gantt`. Through the repository's
  `Directory.Build.targets` these resolve to the live sibling source projects.
- Shared UI families: [CanDoItAll.AppComponents.RecordBrowsing](../CanDoItAll.AppComponents.RecordBrowsing/README.md)
  and `CanDoItAll.AgentFramework.UI` (agent cards).
- Editing: `CanDoItAll.Editing` and `CanDoItAll.Editing.UI` provide pure typed comparisons
  and shared field rendering. The module hosts retain owner reads, writes, authorization,
  subscriptions and close/navigation decisions.
  Opportunity dialogs receive the host's controlled model and opening. Their form
  keeps its `EditContext`, keyed rows and wizard section controls through refresh and
  receipt reconciliation. Typed input/picker callbacks protect pending browser input;
  selecting a project asks the host to capture its lifetime. Comparison Apply and Save
  are separate actions. Board advance emits the stage and version displayed on its card.
  Conversion uses the same controlled-form lifetime. Its comparison accepts a coherent
  source and complete project handoff basis locally, preserving target text; it never
  writes. An unknown conversion retains its recovery project and blocks replay even
  after a peer change or comparison refresh.
- Lightweight contracts: [CRM / HR contracts](../../Modules/CanDoItAll.Modules.CrmHr.Contracts/README.md),
  which brings the [Projects contracts](../../Modules/CanDoItAll.Modules.Projects.Contracts/README.md),
  `CanDoItAll.AgentFramework.Models` and `CanDoItAll.SharedKernel`.

It has no dependency, direct or transitive, on the CRM / HR or Projects module implementations,
Infrastructure, Entity Framework, the AgentFramework runtime, AppComponents, Web or Composition;
`CrmHrUiModuleBoundaryTests` and the three surface boundary classes guard the categories, the
injection policy, the transitive closure and the host/renderer roles.

Effect ownership stays in `CanDoItAll.Modules.CrmHr`: the seven routed pages, their sessions and
mappers, the `AccountSummaryPanel`, `InteractionTimeline`, `CrmFinancialsPanel` and
`AgentRecruitingEvidencePanel` hosts, `CrmAgentChatContextProvider` and the `CrmHrSecondaryTabs`
navigation adapter.

Build from the repository root:

    dotnet build src/UI/CanDoItAll.CrmHr.UI/CanDoItAll.CrmHr.UI.csproj --configuration Release /m:1

The [CRM / HR UI sandbox](../../Sandboxes/CanDoItAll.CrmHr.UiSandbox/README.md) exercises the
renderers with deterministic local state. The completion map, decisions, defects and evidence are
in [CRM / HR UI decoupling: completion record](../../../docs/architecture/crm-hr-ui-completion.md);
the earlier slices keep their own records:
[Home](../../../docs/architecture/crm-hr-home-ui-boundary.md),
[account summary and activity history](../../../docs/architecture/crm-hr-account-activity-ui-boundary.md)
and [Financials](../../../docs/architecture/crm-hr-financials-ui-boundary.md).

`ConnectionReconciliation` and `InteractionAppendState` keep connection replacement and
append-only journal drafts explicit. `ConnectionEditing` renders bounded local review;
`InteractionJournalEditor` keeps one controlled EditContext per opening, immediate raw
inputs and retired callback fences. Native reads, selected project lifetimes, access,
observation and commands remain with the module host.

Workforce profile and keyed skill review use typed B/L/R values and keep one model and
EditContext per opening. Apply and Undo edit the local draft; Save is separate. Foreign
profile hints request an explicit refresh or review even when the form is clean. An
ordinary post-save context read cannot silently replace a different profile basis.
Skills, catalog creation and capacity creation retain submitted and later input separately;
manual refresh retains the forms and their raw validation. Allocation charts remain read-only.

Number and date fields use BaseLib's `NativeValueInput`, whose native input event includes
the browser's incomplete-input flag. An unfinished nullable control stays pending through
blur, another edit and accepted-state reconciliation instead of becoming an empty value.
The shared component example is `/native-value-input` in the Components sandbox. The
delivery-unit dialog reuses the party editor's conditional save and review lifetime.

Recruiting application, support and conversion comparisons use complete native originals
and typed fields. Apply changes the local draft; the native command runs only on Save.
Interview and checklist creation keep the accepted row separate from later input. Each
form retains its model and EditContext through related refreshes; task reset replaces only
the task form. Native date/time controls retain incomplete input across tab changes.
Selected checklist projects carry the displayed lifetime with their identity. Observation,
current access, command receipts and uncertain outcomes remain with the module sessions.

Assignments use typed submission/current/accepted reconciliation for relationships,
allocations and staffing requests. Later input keeps the exact accepted identity and
full-row original. Comparisons read the current native row, preserve unresolved raw
controls and custom comparison text, and apply only to the draft. Unknown outcomes
require a read before explicit local discard; writes are never replayed by refresh.
Visited form tabs remain mounted while unvisited panels stay lazy.

Party and opportunity forms use BaseLib's `NativeFormInspector` for browser validity
and focus. Application renderers own no JavaScript runtime or private inspection asset.
