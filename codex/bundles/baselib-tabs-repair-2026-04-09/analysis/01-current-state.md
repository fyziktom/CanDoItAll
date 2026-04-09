# Current State

## Shared Tabs Component

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Tabs.razor` renders a working tablist and tabpanels with keyboard navigation, disabled-state handling, icon support, `RenderMode`, `TabPosition`, and `TabsVariant`.
- The component currently assigns paired classes such as `cad-tabs__list zy-tabs__list`, `cad-tabs__tab zy-tabs__tab`, and `cad-tabs__panel zy-tabs__panel`.
- `BuildContainerAttributes()` emits both `cad-tabs` and `zy-tabs` wrapper classes plus variant and position modifiers for both families.
- `ResolveTabText()` falls back to `"Tab"` when the item text is missing, which is useful for the requested missing-title example and should remain deliberate, not accidental.
- `TabsItem.razor` supports `Text`, `Icon`, `Disabled`, `Visible`, `BadgeText`, `AdditionalAttributes`, and child content, but does not expose a first-class appearance parameter of its own.

## Styling Split

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Tabs.razor.css` is the only live style source for the shared `Tabs` component today.
- That stylesheet is entirely `zy-tabs*` driven and includes a strong purple-white visual language plus workstation-specific overrides.
- The stylesheet lives in component-scoped CSS instead of the Tailwind source-of-truth pipeline.
- `C:\repositories\CanDoItAll\Tailwind\navigation\tabs.css` already holds related navigation styling for `cda-tab-strip`, `cda-tab-menu`, `cda-tab-summary`, and `cda-inline-tab`, which means the repo already has a canonical Tailwind home for tab-adjacent styles.
- `C:\repositories\CanDoItAll\Tailwind\foundation\theme.css` contains the semantic `--cad-*` token contract the tabs component should align to.

## Sandbox Coverage

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Navigation.razor` includes one Workspace tabs example inside a mixed navigation page.
- That page only partially covers tabs behavior:
- it exercises long text in one scenario
- it exercises disabled state in one scenario
- it does not provide a dedicated tabs route or dense comparison surface
- it does not isolate small-column wrapping, missing title fallback, or look-variant comparisons
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\SandboxCatalogRegistry.cs` currently exposes `/groups/navigation` as the only tabs-related sandbox route through the component MCP.

## Existing Tests And Proof

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AppTabStripTests.cs` covers the separate inline app-tab strip, not the shared BaseLib `Tabs` component.
- No dedicated component test file currently targets `CanDoItAll.Components.BaseLib.Tabs`.
- This thread exposes the CanDoItAll managed watch backend and a Playwright CLI skill, but not a dedicated Playwright MCP tool surface. The browser-proof plan therefore needs to use the terminal Playwright CLI workflow and record that explicitly.

## External Reference

- `C:\repositories\radzen-blazor\Radzen.Blazor\themes\components\blazor\_tabs.scss` shows a mature tab structure with position support, selected-border treatment, and panel boundary logic.
- `C:\repositories\radzen-blazor\RadzenBlazorDemos\Pages\TabsWrap.razor` demonstrates explicit wrap handling for many tabs.
- The Radzen reference should guide behavior and visual clarity only. No Radzen styles, classes, or JS should be copied into BaseLib.
