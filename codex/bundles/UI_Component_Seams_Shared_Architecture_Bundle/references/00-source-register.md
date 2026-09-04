# Source register

## Repository baseline observed during preparation

- Repository: `fyziktom/CanDoItAll`
- Branch: `development`
- Commit observed: `6c02b644acae3f0d05c648d6b169c82acebefea8`
- Commit role: preparation evidence only; child bundles must refresh

## Key current source evidence

- `src/UI/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`
  - currently references shared UI/FileTools packages plus SharedKernel and FileTools
    integration abstractions;
  - currently has no direct feature-module project reference.
- `src/UI/CanDoItAll.AppComponents/README.md`
  - describes AppComponents as an application-owned shell/facade and adapter layer.
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs`
  - includes direct route-page orchestration and direct EF context factory access.
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor.cs`
  - owns loading, selection, dialogs, catalog repair, and chat launch.
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs`
  - coordinates several feature/application services and owns editor section state.
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor.cs`
  - combines provider catalog, editor, operations, and supporting state.
- `src/Modules/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`
  - injects `IServiceProvider`.
- `src/Modules/CanDoItAll.Modules.Projects/Pages/Components/ProjectFilesDialog.razor`
  - coordinates project and FileTools host behavior.
- `src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
  - is a large cross-capability route page with broad direct dependencies.
- `tests/Unit/CanDoItAll.Tests.Unit/ProjectStructurePageArchitectureTests.cs`
  - currently asserts an exact partial file count of `22` and contains several
    source-string architecture assertions.

## Supplied bookmarkability sources

Copied under `inputs/bookmarkability/`:

- `01_meeting_brief.md`
- `02_updated_analysis_and_architecture.md`
- `03_phased_implementation_plan.md`
- `04_evidence_register.md`

These inputs establish:

- URL as authoritative shareable state;
- route identity independent from visual presentation;
- page/workspace ownership of route-significant state;
- controlled child components and typed intents;
- state taxonomy;
- later route-driven overlay, Push/Replace, SSR, Workbench, and MAUI requirements.

## SharedInfo guidance consulted

- `CanDoItAll.SharedInfo/codex/skills/bundles/candoitall-bundle-preparation/SKILL.md`
- `CanDoItAll.SharedInfo/codex/skills/bundles/candoitall-csharp-architecture-bundle-guard/SKILL.md`

This bundle intentionally uses a compatible non-canonical shape because it is an
architecture reference rather than an executable initiative bundle. Child implementation
bundles remain subject to the normal bundle preparation, execution, validation, and C#
architecture guard skills.
