# AgentFramework rendering UI

This Razor class library owns the controlled AgentCatalogPanel, its snapshot/selection/intent contracts, the real AgentSelectionCard and the pure participant presentation mapper. The card and mapper retain their existing namespace for consumer compatibility; their assembly is this UI project.

The capabilities boundary also owns the real AgentCapabilitiesSurface, AgentCapabilityList and immutable selection/load/access/intent/presentation contracts. AgentDetailsDialog and the standalone surface consume the same list. Application operation outcomes, recovery, sessions and Curator launch state remain in the module; the effect host maps them to presentation records.

AgentCatalogHost, AgentCapabilitiesPanel, dialogs, chat launch, persistence and provider/runtime effects remain in the AgentFramework module. The rendering boundary depends on Models, Conversations components and the existing BaseLib UI primitives. Repository source mode supplies live sibling components; this project does not reference the broad AgentFramework.Components assembly.

Build from the repository root:

    dotnet build src/UI/CanDoItAll.AgentFramework.UI/CanDoItAll.AgentFramework.UI.csproj --configuration Release

The [catalog sandbox](../../Sandboxes/CanDoItAll.AgentFramework.UiSandbox/README.md) exercises this same implementation with controlled snapshots. Preserve its real card/tree/tooltips, CSS isolation, fonts and generated theme assets when changing composition. Existing catalog component tests cover public rendering and intents; production host tests cover effects and lifetime.
