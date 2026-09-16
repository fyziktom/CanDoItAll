# CRM / HR rendering UI

This Razor class library owns the real CRM / HR Home overview rendering: the controlled
`CrmHrHomeSurface`, its immutable presentation records (`CrmHrHomePresentation`,
`CrmHrHomeOverview`, totals and the three preview row types), the typed
`CrmHrHomeIntent` family and the pure text helpers. The production route `/crm-hr` and the
scenario sandbox render this same component; there is no second Home renderer. It currently
contains only Home; other CRM / HR pages stay in the module until they get their own
bounded extraction.

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
on the module route catalogue.

Build from the repository root:

    dotnet build src/UI/CanDoItAll.CrmHr.UI/CanDoItAll.CrmHr.UI.csproj --configuration Release /m:1

The [CRM / HR UI sandbox](../../Sandboxes/CanDoItAll.CrmHr.UiSandbox/README.md) exercises the
surface with deterministic local state. The boundary decisions, behavior matrix and
validation record live in
[CRM / HR Home UI boundary](../../../docs/architecture/crm-hr-home-ui-boundary.md).
