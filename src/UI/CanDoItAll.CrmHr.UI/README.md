# CRM / HR rendering UI

This Razor class library owns the real rendering of three bounded CRM / HR areas. Each area is
a controlled surface with immutable presentation records, a typed intent family and pure text
helpers; the production hosts and the scenario sandbox render the same components, so there
is no second renderer for any of them. Other CRM / HR pages stay in the module until they get
their own bounded extraction; the module is not declared decoupled.

- `Home/`: the Home overview (`CrmHrHomeSurface`, `CrmHrHomePresentation`,
  `CrmHrHomeOverview`, totals and the three preview row types, `CrmHrHomeIntent`). Rendered
  by the production route `/crm-hr`.
- `Accounts/`: the CRM account summary (`CrmHrAccountSummarySurface`, `CrmHrAccountSummary`
  with resolved labels, tones and counts, `CrmHrAccountSummaryIntent` = open the directory
  record or convert the named account to an active customer). Rendered by the module's
  `AccountSummaryPanel` adapter inside the CRM account dialog.
- `Activity/`: the activity history timeline (`CrmHrActivitySurface`, `CrmHrActivityPage`
  with the whole-history counts and one page of `CrmHrActivityEntry` rows,
  `CrmHrActivityCopy` for host-specific wording, `CrmHrActivityIntent` = request a page).
  Rendered by the module's `InteractionTimeline` adapter in its three compositions: CRM
  account activity, Directory party history and Workforce history.

Surfaces carry an `IsInteractive` input where a host renders them before its circuit can
handle events: the host flips it after its first interactive render, the root reports
`data-interactive`, and the actions stay disabled until then.

The library references `CanDoItAll.Components.BaseLib` (which brings
`CanDoItAll.Components.Common`) and `Microsoft.AspNetCore.Components.Web`. It has no
dependency on the CRM / HR module implementation, Infrastructure, Entity Framework, Web,
AgentFramework runtime or application registration, and it declares no contracts of its
own beyond the Home presentation records. No component injects a service; the surface
receives explicit presentation data and emits intents.

Effect ownership stays in `CanDoItAll.Modules.CrmHr`: `CrmHrHomePage` owns the route, the
document title, the static agent-context surface, navigation through `CrmHrRouteCatalog`
and the `CrmHrHomeReadSession` that reads `ICrmHrHomeQueryService` once per instance with
explicit Loading, Ready and Failed phases; `CrmHrHomePresentationMapper` projects the query
snapshot into the presentation records. The production secondary tabs are host-owned chrome
rendered through the surface's `SecondaryNavigation` slot because they navigate and depend
on the module route catalogue. For the account summary and the timeline, the CRM, Directory
and Workforce pages keep their reads, loading and failure states, paging requests, the
directory navigation and the "convert to active customer" mutation (`CrmHrCrmPage` captures
its action stamp and saves the profile through `CrmService`); `CrmHrAccountSummaryMapper` and
`CrmHrActivityPresentationMapper` project the workspace model and the history page into the
presentation records.

Build from the repository root:

    dotnet build src/UI/CanDoItAll.CrmHr.UI/CanDoItAll.CrmHr.UI.csproj --configuration Release /m:1

The [CRM / HR UI sandbox](../../Sandboxes/CanDoItAll.CrmHr.UiSandbox/README.md) exercises the
surfaces with deterministic local state. The boundary decisions, behavior matrices and
validation records live in
[CRM / HR Home UI boundary](../../../docs/architecture/crm-hr-home-ui-boundary.md) and
[CRM / HR account summary and activity history UI boundary](../../../docs/architecture/crm-hr-account-activity-ui-boundary.md).
