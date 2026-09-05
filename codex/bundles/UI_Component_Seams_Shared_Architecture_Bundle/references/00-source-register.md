# Source register and limits

## Observed revision

- Repository: fyziktom/CanDoItAll
- Branch: components-decoupling
- Reviewed HEAD: a249d77b175916d760e9f6c86633202a4ea3ae44
- Earlier shared-base observation: development at 6c02b644acae3f0d05c648d6b169c82acebefea8
- Components source inspected: c3e6aa03a878994c0ba8aed6af017d0be75f3796
- These are observations, not execution pins. Product source had no changes between the
  Agents preparation observation c225bf2445835bf12fa5054bc15571d2ce23b4fe and reviewed HEAD;
  that diff added the Agents bundle.

CodeAnalytics snapshot snap-20260904231957-7bf47433 covered AgentFramework, AppComponents,
and Web: 3 source projects, 308 documents, 858 types, 8001 members. It loaded without
blocking errors, with duplicate generated-type warnings and partially interpreted DI
factories. Its scoped references are not a complete evaluated transitive project graph.
Direct csproj, Directory.Build.targets, component, and test inspection supplied that context.
Components MCP transport was closed during review; sibling component source was inspected.

## Primary evidence

Paths below are relative to the product repository unless marked sibling:
- src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs:
  separate history-host suppression, dashboard and usage loads, and chat context.
- src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor.cs:
  selection/open distinction, result channels, team operations and context readiness.
- src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs:
  mutable draft, load/save/clear, partial catalogs, capability persistence and child operations.
- src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs and
  src/Modules/CanDoItAll.Modules.Security/SecurityModels.cs: picker records in implementation assemblies.
- src/MAF/Common/CanDoItAll.AgentFramework.Models/Editors/EditorModels.cs:
  mutable editor and ExpectedUpdatedAtUtc.
- src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.Agents.cs:
  concurrency and persistence semantics.
- tests/Components/CanDoItAll.Tests.Components/ProviderAdministrationLayoutTests.cs:
  two history-host cases protect no eager aggregate/history reads.
- src/UI/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj:
  no concrete feature-module reference.
- src/UI/CanDoItAll.Conversations.Components/CanDoItAll.Conversations.Components.csproj and
  src/UI/CanDoItAll.Conversations.Shell/CanDoItAll.Conversations.Shell.csproj:
  existing reusable application UI family.
- src/App/CanDoItAll.Web/Components/App.razor and Directory.Build.targets:
  InteractiveServer host, explicit asset wiring, live sibling conversion.
- Sibling CanDoItAll.Components/src/CanDoItAll.Components.BaseLib/Components/Modals/DialogService.cs:
  LocationChanged closes all imperative dialogs.

The initial hotspots inventory is illustrative, not an exhaustive current UI audit.

## Inputs and technical references

All four Markdown sources under inputs/bookmarkability matched the supplied
CanDoItAll_Bookmarkability_Meeting_Pack_2026-09-04.zip during review. Preserve them unchanged.
Their product decisions remain proposals; v2 records the accepted review separately.

- [dotnet watch graph behavior](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch)
- [Blazor DI lifetime](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection?view=aspnetcore-10.0)
- [Blazor navigation](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/navigation?view=aspnetcore-10.0)

Consulted current bundle preparation/validation, C# architecture governor/bundle guard,
test selection/proof, Components composition, and SharedInfo source-of-truth guidance.
No runtime, browser, or performance claim is established by the static review.
