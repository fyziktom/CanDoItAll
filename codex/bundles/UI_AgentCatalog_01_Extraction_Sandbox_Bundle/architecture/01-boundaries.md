# Concrete boundary and dependency decisions

Current source:
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor, .razor.cs and .razor.css.
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogState.cs: AgentCatalogSnapshot/Selection/Intent.
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentSelectionCard.razor and AgentParticipantPresentationMapper.cs, including AgentParticipantCardProjectionOptions.
- repo://src/UI/CanDoItAll.Conversations.Components/ConversationParticipantCard.razor and its isolated CSS.
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogHost.razor and Services/AgentCatalogOperations.cs stay outside.
- Pure stable managed identities, team icons, workload display and agent model contracts already live in AgentFramework.Models. Do not invent replacement identity policies or a second catalog DTO graph.

The current module declares 43 project references and three Components package references replaced by sibling source (46 effective direct references). Existing AgentFramework.Components pulls Core and Voice plus CanvasLib. A new rendering assembly must not reference that broad project merely to use its card.

Chosen move: create CanDoItAll.AgentFramework.UI (Razor, net10.0) owning the real catalog panel and contracts, the real AgentSelectionCard and its existing pure presentation mapper/options. Preserve the card/mapper public namespace to avoid unrelated consumer churn; use a clear new catalog namespace and update direct host/test imports. Move source, do not link/compile it into both old and new assemblies.

References:
- new UI -> AgentFramework.Models, Conversations.Components, BaseLib (live sibling), ASP.NET Components.Web.
- old AgentFramework.Components -> new UI for remaining compact/switch consumers.
- module AgentFramework -> new UI, and its existing effects/runtime dependencies.
- new UiSandbox -> new UI plus minimal BaseLib/Blazor hosting.
- never new UI/sandbox -> module AgentFramework, old AgentFramework.Components, Core, Voice, EF or production composition.

Conversations.Components currently references Markdig, BaseLib and OverlayLib; retain its real implementation and measured dependency cost. Models references its existing small abstractions/SharedKernel graph. Do not claim zero dependencies, eliminate real children or move feature code into AppComponents.

AgentSwitchDialog calls the mapper's currently internal MapCard/options. Make that pure presentation API/options explicitly public when moving it, and build all existing callers; do not duplicate mapping or add a friend dependency back into the broad component assembly. AgentCompactList/Item and AgentConversationShellContributor continue using the same mapper. No behavioral rewrite of those consumers.

State remains controlled: panel owns search/expansion only, host owns authoritative selection and effects. No new controllers/interfaces are needed. The sandbox supplies immutable representative snapshots and applies typed selection/records intents locally; effect intents are displayed as sandbox feedback, never connected to real services.

Before execution refresh scoped CodeAnalytics, evaluated references and exact source hashes. Provider closure snapshot snap-20260905200636-831cc390 is historical input, not a post-extraction graph. Any new dependency cycle or forced broad runtime reference reopens SB01; do not solve it by importing a service bag.
