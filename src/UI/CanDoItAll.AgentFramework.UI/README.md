# AgentFramework catalog UI

This Razor class library owns the controlled AgentCatalogPanel, its snapshot/selection/intent contracts, the real AgentSelectionCard and the pure participant presentation mapper. The card and mapper retain their existing namespace for consumer compatibility; their assembly is this UI project.

AgentCatalogHost, dialogs, chat launch, persistence and provider/runtime effects remain in the AgentFramework module. The rendering boundary depends on Models, Conversations components and the existing BaseLib UI primitives. Repository source mode supplies live sibling components; this project does not reference the broad AgentFramework.Components assembly.

Build from the repository root:

    dotnet build src/UI/CanDoItAll.AgentFramework.UI/CanDoItAll.AgentFramework.UI.csproj --configuration Release

The [catalog sandbox](../../Sandboxes/CanDoItAll.AgentFramework.UiSandbox/README.md) exercises this same implementation with controlled snapshots. Preserve its real card/tree/tooltips, CSS isolation, fonts and generated theme assets when changing composition. Existing catalog component tests cover public rendering and intents; production host tests cover effects and lifetime.
