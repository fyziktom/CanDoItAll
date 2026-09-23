# AgentFramework rendering UI

This Razor class library owns the controlled AgentCatalogPanel, its snapshot/selection/intent contracts, the real AgentSelectionCard and the pure participant presentation mapper. The card and mapper retain their existing namespace for consumer compatibility; their assembly is this UI project.

The capabilities boundary also owns the real AgentCapabilitiesSurface, AgentCapabilityList and immutable selection/load/access/intent/presentation contracts. AgentDetailsDialog and the standalone surface consume the same list. Application operation outcomes, recovery, sessions and Curator launch state remain in the module; the effect host maps them to presentation records.

AgentCatalogHost, AgentCapabilitiesPanel, dialogs, chat launch, persistence and provider/runtime effects remain in the AgentFramework module. The rendering boundary depends on Models, Usage value contracts, Conversations components and the existing BaseLib/Charts UI primitives. Repository source mode supplies live sibling components; this project does not reference the broad AgentFramework.Components assembly.

Build from the repository root:

    dotnet build src/UI/CanDoItAll.AgentFramework.UI/CanDoItAll.AgentFramework.UI.csproj --configuration Release

The [catalog sandbox](../../Sandboxes/CanDoItAll.AgentFramework.UiSandbox/README.md) exercises this same implementation with controlled snapshots. Preserve its real card/tree/tooltips, CSS isolation, fonts and generated theme assets when changing composition. Existing catalog component tests cover public rendering and intents; production host tests cover effects and lifetime.

## Overview boundary

The real AgentsOverviewSurface, immutable AgentsOverviewState/intents, pure presentation mapper/options, ProviderUsageConsumerList and AgentUsageDisplay live in `Overview`. The formatting helper is public because the retained Module usage dialogs and AgentOverviewUsageList consume this same implementation. No wrapper or second renderer remains in Module.

AgentsHomePage owns the page-lifetime session, independent Header/Overview/Usage reads, route state, HR/defaults/team effects and its three usage-dialog lifetimes. The UI library registers no application queries, usage projection sources, persistence or runtime effects. Existing application snapshot types remain in Models/Usage and are copied into immutable presentation at the effect boundary.

The existing sandbox has an Overview specimen with actual Charts registration/assets and controlled state. Long model labels belong to the retained ModelUsageDialog: the Overview dashboard itself does not render model rows. Browser validation covers that real dialog rather than adding a different model renderer to the sandbox.
