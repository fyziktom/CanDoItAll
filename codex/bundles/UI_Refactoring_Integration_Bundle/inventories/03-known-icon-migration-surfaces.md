# Known Icon Migration Surfaces

Refresh with repository-wide search after merging development. The discovery set is:

## Host asset

1. `src/App/CanDoItAll.Web/Components/App.razor`

## Raw icon DOM

2. `src/UI/CanDoItAll.AppComponents/Components/TunableComponentBoundary.razor`
3. `src/Modules/CanDoItAll.Modules.Plugins/Pages/PluginsPageHelpers.cs`
4. `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/MaterialIconPickerDialog.razor`
5. `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor`
6. `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentTeamDetailsDialog.razor`

## CSS selectors

7. `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.css`
8. `src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureProcessAgentPickerDialog.razor.css`

## bUnit selectors

9. `tests/Components/CanDoItAll.Tests.Components/AgentCompactListTests.cs`
10. `tests/Components/CanDoItAll.Tests.Components/Conversations/PresentationBadgeListTests.cs`
11. `tests/Components/CanDoItAll.Tests.Components/AgentCatalogPanelTests.cs`

## Migration rules

- Prefer `<Icon Name="...">` in Razor.
- For `RenderTreeBuilder`, use `cda-material-icon material-symbols-rounded` and preserve
  accessibility semantics.
- CSS and tests target `.cda-material-icon`.
- Do not target `.material-symbols-rounded` as the long-term semantic selector.
- Preserve `data-testid`, ARIA, button labels, and icon token values.
- After migration:

```bash
rg -n "material-icons|material-icons\.css" src tests Tailwind
```

must return no unintended result.
