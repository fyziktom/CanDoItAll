# Source scope inventory

## Existing production files expected to change

```text
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentWorkspaceTabs.cs
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentWorkspaceRouteState.cs
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor.cs
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs
src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkUiServiceCollectionExtensions.cs
```

## New production files permitted

New top-level types inside the existing AgentFramework module for:

- workspace section/state and mapping;
- details section/request and order mapping;
- overview query contract/result/implementation;
- catalog snapshot/view state/intent/controller;
- editor session/controller and focused pure mapping/normalization types.

Do not add new partials or projects. Prefer cohesive files grouped by responsibility; do
not create one file/type for every one-line record when a small contract family is clearer.

## Test files expected to change

See `inventories/02-test-impact-and-classification.md`.

## Explicitly forbidden production scope

- `src/UI/CanDoItAll.AppComponents/**`;
- sibling Components/FileTools repositories;
- AgentProviderProfilesPanel and other AgentFramework tabs except call-site adaptation;
- other modules' production code;
- root build graph, Directory.Build files, solution files, and package versions;
- Manager, Tailwind, Docker, migrations, and routes outside current compatibility mapping.
