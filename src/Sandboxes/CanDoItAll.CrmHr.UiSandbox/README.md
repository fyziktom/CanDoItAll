# CRM / HR UI sandbox

Backend-free scenario host for the real CRM / HR surfaces from
[CanDoItAll.CrmHr.UI](../../UI/CanDoItAll.CrmHr.UI/README.md): the Home overview at
`/crm-hr`, the account summary with the activity history at `/crm-hr/account-activity`, the
Financials projection at `/crm-hr/financials`, and the seven routed workspace surfaces at
`/crm-hr/workspaces/*` (see below). It references only that rendering library (and, through
it, BaseLib, Common, AppComponents.RecordBrowsing, AgentFramework.Models/UI and the chart and
Gantt libraries; `Program.cs` registers the charts like the Web host — Gantt and the
AgentFramework agent cards are plain presentational libraries with no DI registration of
their own). It has no database, no real query service, no Web, module, AgentFramework runtime
or navigation dependency: `CrmHrSandboxReadPorts.cs` implements the five read ports the
workspace surfaces inject (`IPartyRecordQueryService`, `IWorkforceRecordQueryService`,
`IOpportunityPipelineQueryService`, `IPartyOrganizationAffiliationReader`,
`IProjectRecordQueryService`) as deterministic in-memory services over one shared synthetic
data set (`CrmHrSandboxData`: about 30 parties of mixed types, 8 projects, opportunities for
Northwind Logistics and organization affiliations for two people), registered through
`AddCrmHrSandboxReadPorts()` so tests can register the same fakes `Program.cs` uses. The party
and opportunity catalogs are backed by mutable in-memory stores (`CrmHrSandboxPartyStore`,
`CrmHrSandboxOpportunityStore`) so a save, merge or delivery-unit creation is visible in the
real catalogs and pickers on their next read, the same way real persistence would behave.
Every interaction updates deterministic local state and the intent line; the production
secondary tabs are host-owned chrome and are represented by a labelled slot, not duplicated.

Run from the repository root with the SDK selected by `global.json` and the live Components
checkout. Open the browser at 1600x1000 for the matched desktop frame.

## Parity: production assets

```sh
npm ci --prefix Tailwind
npm run tailwind:build
npm run crmhr:watch:parity
```

Open http://127.0.0.1:5397/crm-hr. Parity links the real production CSS with its physical
ContentRoot plus the BaseLib CSS, fonts, icons and scoped component CSS. It requires the
production theme to exist; a missing theme fails the build explicitly. Equivalent direct
command:

```sh
dotnet watch --project src/Sandboxes/CanDoItAll.CrmHr.UiSandbox --launch-profile "CrmHr sandbox" --property:CrmHrAssetMode=Parity
```

## Fast: local assets and bounded scanning

`npm ci --prefix Tailwind` is a one-time prerequisite of both modes on a fresh clone. The generated
stylesheet is ignored, so building this sandbox in Fast mode before it exists fails with exactly
these two commands in its message.

```sh
npm ci --prefix Tailwind
npm run crmhr:css:build
npm run crmhr:watch:fast
```

Open http://127.0.0.1:5398/crm-hr. Fast generates the ignored `wwwroot/css/crmhr-fast.css`
from `Tailwind/crmhr-fast.css`, which scans only the rendering library and this sandbox. It
never writes the production theme and can build when that asset is absent. Both modes use
separate bin/obj directories; a contradictory runtime profile fails explicitly.

## Scenarios

`/crm-hr?scenario=<token>&layout=matched|narrow|flexible`. Unknown tokens fall back to
`populated` and `matched`. Controls replace the current history entry so a reload restores
the context; the intent line stays transient.

| Token | Presentation |
|---|---|
| `populated` | Totals above the preview caps, five directory rows, one sensitive row, three opportunities with and without amounts |
| `loading` | Loading phase: placeholder stat values, loading state, route buttons still usable |
| `empty` | A successful read with zero totals: the section-specific empty copy |
| `failed` | Failed phase with safe copy and Retry; Retry resolves to the populated overview |
| `sensitive` | Several sensitive rows; the sensitive card stays narrower than the directory rows |
| `long-text` | Markup-looking and very long names, summaries and titles rendered as text |
| `null-optionals` | Missing summaries, no sensitive rows, an opportunity without an amount and with an unknown owner |
| `large-totals` | Totals in the hundreds and thousands while the previews stay capped at 5 / 3 / 6 |

## Account summary and activity history

`/crm-hr/account-activity?scenario=<token>&layout=matched|narrow|flexible` renders the
account summary followed by the timeline in its three production compositions (CRM account
activity `crmhr-account-activity`, Directory party history `crmhr-directory-activity`,
Workforce history `crmhr-workforce-history`), each with its own wording and its own page
index over the same deterministic history (page size 10). The conversion intent updates the
local account to an active customer; the production page saves and reloads instead.

| Token | Presentation |
|---|---|
| `populated` | Prospect account with contacts, roles and counts; twelve entries over two pages with one overdue follow-up |
| `no-account` | The no-account empty state with its directory action; empty timelines |
| `active-customer` | An active customer: no conversion offer |
| `loading` | First read in flight: nothing accepted, counts shown as loading, paging disabled |
| `paging` | Another page loading over an accepted first page: totals kept, rows replaced by the loading state |
| `empty` | Zero entries accepted: each host's own empty copy, accepted zero counts and "No pages" |
| `overdue` | Seven follow-ups, four overdue: the overdue total and per-row Overdue badges |
| `long-text` | Markup-looking and very long names, summaries, titles and metadata rendered as text |
| `null-contacts` | No summary, no email, no phone, no roles, zero counts: the placeholder copy |
| `many-pages` | Forty-seven entries over five pages for independent paging per host |

## Financials

`/crm-hr/financials?scenario=<token>&layout=matched|narrow|flexible` renders the Financials
surface with the real chart. Retry resolves the failed scenario to the populated snapshot
locally; Monthly/Yearly re-project the same snapshot and issue no read.

| Token | Presentation |
|---|---|
| `populated` | Three currencies over two years in sparse months (no currency sells every month): the aligned category axis |
| `single-currency` | One currency over three consecutive months |
| `empty` | An accepted empty result: "No recognized sales", no chart, purchase and invoice figures unavailable |
| `incomplete` | The populated snapshot with three incomplete won records |
| `loading` | The loading phase |
| `failed` | The failed phase with Retry |
| `long-labels` | Eighteen consecutive months in two currencies with seven-figure amounts and twelve incomplete records |

## Workspace surfaces

`/crm-hr/workspaces/<area>?scenario=<token>&layout=matched|narrow|flexible` renders one real
routed workspace surface with a sandbox view implementing its `ICrmHr…WorkspaceView` contract
(`CrmHr…SandboxView` in `CrmHr…WorkspaceSandbox.cs`) and the same header controls as the other
specimens. `SecondaryNavigation` on every workspace is filled with the real
`CanDoItAll.CrmHr.UI.Shared.CrmHrAreaTabs`; its `AreaSelected` callback is host-owned
navigation and only logs `Navigate: <route>` here. `ActivityTimeline` (Directory, Workforce,
Crm) and `AccountSummary`/`Financials` (Crm) reuse the existing `CrmHrAccountActivitySandboxFixture`
and `CrmHrFinancialsSandboxFixture` at their `Populated` scenario; they gate their own
loading/failed presentation independently of the workspace's own scenario token.

| Route | Surface | Read ports it exercises |
|---|---|---|
| `/crm-hr/workspaces/directory` | `CrmHrDirectoryWorkspaceSurface` | Party (catalog, `PartyRelationshipsEditor`, `PartyAffiliationsEditor`), affiliation reader |
| `/crm-hr/workspaces/crm` | `CrmHrCrmWorkspaceSurface` | Party (catalog, connection picker), project (`ProjectMultiPicker`), opportunity pipeline |
| `/crm-hr/workspaces/workforce` | `CrmHrWorkforceWorkspaceSurface` | Workforce (catalog), party (`PartyPicker` for home unit / manager) |
| `/crm-hr/workspaces/recruiting` | `CrmHrRecruitingWorkspaceSurface` | Party (`PartyPicker` for conversion home unit / manager); the application catalog itself is view-owned |
| `/crm-hr/workspaces/agents` | `CrmHrAgentsWorkspaceSurface` | None; the projected agent catalog is view-owned |
| `/crm-hr/workspaces/assignments` | `CrmHrAssignmentsWorkspaceSurface` | Project (`AssignmentsProjectContextBar`'s picker); assignments, staffing and candidates are view-owned |
| `/crm-hr/workspaces/agent-evidence` | `AgentRecruitingEvidenceSurface` | None; interviews, attempts and readiness are view-owned |

Scenarios per workspace (`Catalog` is always the default token). A scenario is skipped only
when the contract cannot express it; the reason is noted below and, for Agents, at the top of
`CrmHrAgentsWorkspaceSpecimen.razor`.

| Workspace | Scenarios |
|---|---|
| Directory | `catalog`, `selected-existing` (Jonas Keller, long stewardship note), `new-draft`, `non-default-section` (Relations tab with duplicate candidates), `loading`, `failed`, `unavailable-references` (an affiliation, relationship and project assignment pointing at records no read port returns), `empty` |
| Crm | `catalog`, `selected-existing` (Northwind Logistics), `new-draft` (opportunity create dialog), `non-default-section` (Opportunities tab, selected opportunity), `loading`, `failed`, `unavailable-references` (a connected record pointing at a missing party), `empty` (Beacon Hill Advisory), `busy` (`IsOpportunityBusy` mid-save) |
| Workforce | `catalog`, `selected-existing` (Elena Ward), `new-draft` (external contact requesting a staffable profile), `non-default-section` (Allocations tab with capacity blocks and the read-only Gantt), `loading`, `failed`, `unavailable-references` (a capacity block and home unit/manager pointing at unavailable records), `empty` (Ivy Chapman, real zeros), `busy` (`IsDeliveryUnitSaving`) |
| Recruiting | `catalog`, `selected-existing` (Lucas Meyer, Interviewing), `new-draft`, `non-default-section` (Interviews tab), `unavailable-references` (recruiter/hiring manager/unit shown as unavailable), `empty` (Ava Thompson, no interviews or tasks). `loading` is skipped: `LoadApplicationPageAsync` is view-owned and returns synchronously, so the contract exposes no independently controllable dialog-loading flag. `failed` shows the real `RecruitmentConversionPolicy` ineligibility error on the Conversion tab instead of a load failure, since the contract has no load-failure/retry gate for the dialog itself. |
| Agents | `catalog`, `selected-existing` (Atlas Ops Agent, bound and approved), `loading` (`ShouldShowListLoadingState` and `IsWorkspaceLoading`), `unavailable-references` (Scheduler Agent, unbound, no provider), `empty` (real zero catalog). `new-draft` and `non-default-section` are skipped: the surface has no create action (agents are provisioned only in AgentFramework) and `CrmHrAgentRecordDialog` owns its tab index internally, outside `ICrmHrAgentsWorkspaceView`. `failed` is skipped: `HandleAgentDirectoryLoadFailed` has no corresponding failure-message property to render. |
| Assignments | `catalog`, `selected-existing` (Northwind Warehouse Automation: people and an AI agent assigned, a staffing request, an allocation and Gantt data), `new-draft` (relationship draft prepared), `non-default-section` (Allocations tab), `loading` (`IsProjectContextLoading`), `unavailable-references` (an assignment row pointing at a missing party), `empty` (Beacon Hill Advisory Engagement, real zeros). `failed` is skipped: `HandleProjectPickerLoadFailed` has no corresponding failure-message property to render; a separate `busy` token is skipped as redundant with `loading`, the contract's only busy-like signal. |
| Agent evidence | `no-candidate`, `loading`, `failed`, `ready` (interviews with attempts and analysis), `create-dialog-open`, `attach-dialog-open` |

**Regression note**: the Assignments surface binds `ProjectPartyAssignmentPanel.SearchText`,
`StaffingRequestEditor.SearchText`, `ProjectAllocationEditor.SearchText` and
`.CandidateSearchText` to `View.RelationshipAssignmentSearchText`, `View.StaffingRequestSearchText`,
`View.AllocationAssignmentSearchText` and `View.CandidateSearchText` respectively; a fixed
production defect once passed the literal parameter names as search text instead. The
Assignments specimen's interaction test types into all four inputs and asserts the rendered
value is never one of those literal identifiers.

Fast CSS (`Tailwind/crmhr-fast.css`) scans only this sandbox and the rendering library; no
`@source` addition was needed for the workspace specimens since they compose only components
already covered by the library scan.

All data is synthetic. No real person, organization, contact detail, confidential note or
financial record is present, and nothing here reaches an agent context.
