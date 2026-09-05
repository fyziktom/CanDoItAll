# Source scope

All paths below are repository-relative evidence and future scope, not edits authorized by the current documentation request.

## Primary production files

~~~text
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentWorkspaceTabs.cs
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentWorkspaceRouteState.cs
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor.cs
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs
src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkUiServiceCollectionExtensions.cs
~~~

Permit new same-module top-level contract families, state/normalization policies, cohesive operations, production adapters and focused host coordination. Preserve public entry-point behavior; adapt all direct callers/tests together. No new partial files or projects.

## Descendant closure

SB01/SB04 must inspect every rendered or interaction-only child listed in [subtree inventory](04-rendered-subtree-and-contract-closure.md) and discover omitted children. Necessary same-AgentFramework-module child seam edits are within the future behavioral scope once the exact file/scenario is recorded before editing. Existing cross-module children may be exercised through their current interfaces/fakes.

Cross-module or sibling production changes, ownership moves and public API changes are not silently included. If a required parent behavior cannot be preserved without one, document the concrete dependency blocker and scope decision before dependent edits; do not stub away the real scenario to claim closure.

## Tests and direct callers

Use [test inventory](02-test-impact-and-classification.md), including AgentsHomePageTestExtensions and shared details/Memory/storage fixtures. Search all component callers and registrations at SB01; adapt only directly affected consumers. Add focused operation/composition tests inside existing test projects.

## Read-only dependency evidence

Inspect AgentFramework and UI project references, Directory.Build.targets, Web App.razor/static assets, Projects/ProjectModels.cs, Security/SecurityModels.cs, MAF model/core persistence contracts, existing Conversations.Components, Components dialogs/tabs, and FileTools abstractions.

No production edits to AppComponents, other module internals, sibling repositories, root build/solution/package files, Templates, migrations, Docker, Manager, watch infrastructure, or unrelated AgentFramework panes. A separate owned bundle handles physical extraction and its project/assets changes.
