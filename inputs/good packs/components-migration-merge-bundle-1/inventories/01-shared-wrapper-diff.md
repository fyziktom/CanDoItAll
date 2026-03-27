# Shared Wrapper Diff Inventory

## Current Projects

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components`
- `C:\repositories\Zyphonote\src\App.Components\Radzen\Blazor`

## Hash Comparison Result

- identical files: 18
- different files: 22
- Zyphonote-only files: 3
- CanDoItAll-only files: 0

## Identical Files

These can be migrated mechanically after namespace and project relocation:

- `Alert.razor`
- `CategoryAxis.razor`
- `Chart.razor`
- `ContextMenu.razor`
- `DataGrid.razor`
- `DataGridColumn.razor`
- `Dialog.razor`
- `Fieldset.razor`
- `GridLines.razor`
- `Header.razor`
- `Layout.razor`
- `LineSeries.razor`
- `Notification.razor`
- `ProgressBar.razor`
- `Sidebar.razor`
- `Slider.razor`
- `Tooltip.razor`
- `ValueAxis.razor`

## Different Files

These should use the Zyphonote implementation as the merge baseline because it already contains stronger accessibility, customization, or state behavior.

- `Body.razor`
- `Button.razor`
- `Card.razor`
- `CheckBox.razor`
- `Column.razor`
- `DropDown.razor`
- `DropDownOption.cs`
- `FontAwesomeIconCatalog.cs`
- `FormField.razor`
- `Icon.razor`
- `Numeric.razor`
- `Password.razor`
- `Row.razor`
- `Stack.razor`
- `Steps.razor`
- `StepsItem.razor`
- `Switch.razor`
- `Tabs.razor`
- `TabsItem.razor`
- `TextArea.razor`
- `TextBlock.razor`
- `TextBox.razor`

## Zyphonote-Only Files

- `C:\repositories\Zyphonote\src\App.Components\CssClassBuilder.cs`
- `C:\repositories\Zyphonote\src\App.Components\StyledComponentBase.cs`
- `C:\repositories\Zyphonote\src\App.Components\Radzen\RadzenPrimitives.cs`
- `C:\repositories\Zyphonote\src\App.Components\Radzen\Blazor\StepsNavigationPosition.cs`
- `C:\repositories\Zyphonote\src\App.Components\Radzen\Blazor\Tabs.razor.css`

## Key Behavioral Improvements Found In Zyphonote

- `Button.razor`
  - supports anchor mode
  - supports `ChildContent`
  - merges classes/styles cleanly through `StyledComponentBase`
  - exposes extra compatibility looks that should be staged, not blindly promoted
- `CheckBox.razor`, `DropDown.razor`, `Numeric.razor`, `Password.razor`, `TextArea.razor`, `TextBox.razor`
  - add `Disabled`
  - add `InputLook`
- `Tabs.razor`
  - full keyboard navigation
  - `TabPosition`
  - `TabsVariant`
  - `Visible` and `Disabled` tab support
  - badge text support
  - better ARIA wiring
  - isolated CSS source
- `Steps.razor`
  - disabled steps
  - top/bottom/both navigation positioning
  - improved visual state handling
- `Fieldset.razor`
  - stronger ARIA label support
- `TextStyle` in Zyphonote primitives is broader than CanDoItAll’s current version

## Merge Decision

- Use Zyphonote source as the baseline for the 22 differing shared wrapper files.
- Split `RadzenPrimitives.cs` into:
  - `Common`: only neutral low-level items
  - `BaseLib`: component-specific enums, notification service, attribute helpers, icon helpers
- Rename away from `Radzen.*` for the final shared library.
- Keep temporary compatibility shims app-local if that reduces churn.
