# BaseLib Subfolder Organization

## Required Folder Taxonomy

`C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib`

### `Components\Buttons`

- `Button.razor`
- shared button support enums
- any promoted card-action button or pill-button variants that survive as real shared components

### `Components\Badges`

- `StatusBadge.razor`
- promoted `Badge`, `BadgeGroup`, `Chip`, `ChipRow`, `Pill`, and related typed tones

### `Components\Cards`

- `Card.razor`
- `SectionCard.razor`
- promoted shared card shells such as panel, sheet, hero, metric, and action surfaces
- summary and stat tiles that are real reusable card families

### `Components\Forms`

- `FormField.razor`
- `FormSection.razor`
- `CheckBox.razor`
- `DropDown.razor`
- `Numeric.razor`
- `Password.razor`
- `Slider.razor`
- `Switch.razor`
- `TextArea.razor`
- `TextBox.razor`
- promoted form rows, form stacks, sheet/profile field shells, tag editor, and settings rows

### `Components\Feedback`

- `Alert.razor`
- `EmptyState.razor`
- `LoadingState.razor`
- `Notification.razor`
- `HelpPopover.razor`
- `Tooltip.razor`
- promoted callout variants and any feedback host support types

### `Components\Lists`

- `ListDetailShell.razor`
- `ListPanelHeader.razor`
- `SelectionListItem.razor`
- promoted `ListGroup`, `ListItem`, `PlainList`, `MetaList`, and fact-table style display components

### `Components\Layout`

- `Body.razor`
- `Column.razor`
- `Layout.razor`
- `Row.razor`
- `Sidebar.razor`
- `Stack.razor`
- `StickyActionFooter.razor`
- `PageScaffold.razor`
- promoted `PageShell`, workspace panel/split surfaces, and simple layout wrappers that remain justified

### `Components\Modals`

- `Dialog.razor`
- promoted modal shell types replacing `ZyWorkspaceModal`

### `Components\Navigation`

- `ContextMenu.razor`
- `FilterBar.razor`
- `PageHeader.razor`
- `SecondaryTabs.razor`
- `Steps.razor`
- `Tabs.razor`
- promoted toolbar families and ribbon tabs

### `Components\Typography`

- `Header.razor`
- `TextBlock.razor`
- optional thin heading wrappers only if they add real semantic value

### `Components\DataVisualization`

- `Chart.razor`
- `CategoryAxis.razor`
- `LineSeries.razor`
- `ValueAxis.razor`
- `DataGrid.razor`
- `DataGridColumn.razor`

### `Components\Identity`

- `Icon.razor`
- promoted avatar and identity-line components if they survive as real shared API

### Root Or `Infrastructure`

- `StyledComponentBase.cs`
- `ComponentNamespaceMarker.cs`
- service registration extensions
- only truly cross-family infrastructure

## Organization Rules

- Do not leave all components flat under `Components`.
- Do not keep unrelated enums in a single junk-drawer file.
- Put a support type beside the family that owns it.
- Keep temporary compatibility shims in a clearly named `Compatibility` subfolder and delete them once Zyphonote consumers are moved.
- Add explicit `@namespace CanDoItAll.Components.BaseLib` to Razor files so folder moves do not create namespace churn.

## Current `BaseLib` Components That Should Move Immediately

| Current file | Target folder |
| --- | --- |
| `Button.razor` | `Components\Buttons` |
| `StatusBadge.razor` | `Components\Badges` |
| `Card.razor`, `SectionCard.razor`, `SummaryTile.razor`, `SummaryTiles.razor` | `Components\Cards` |
| `FormField.razor`, `FormSection.razor`, input wrappers | `Components\Forms` |
| `Alert.razor`, `EmptyState.razor`, `LoadingState.razor`, `Notification.razor`, `HelpPopover.razor`, `Tooltip.razor` | `Components\Feedback` |
| `ListDetailShell.razor`, `ListPanelHeader.razor`, `SelectionListItem.razor` | `Components\Lists` |
| `Body.razor`, `Column.razor`, `Layout.razor`, `Row.razor`, `Sidebar.razor`, `Stack.razor`, `StickyActionFooter.razor`, `PageScaffold.razor` | `Components\Layout` |
| `Dialog.razor` | `Components\Modals` |
| `ContextMenu.razor`, `FilterBar.razor`, `PageHeader.razor`, `SecondaryTabs.razor`, `Steps.razor`, `Tabs.razor` | `Components\Navigation` |
| `Header.razor`, `TextBlock.razor` | `Components\Typography` |
| `Chart.razor`, `CategoryAxis.razor`, `LineSeries.razor`, `ValueAxis.razor`, `DataGrid.razor`, `DataGridColumn.razor` | `Components\DataVisualization` |
| `Icon.razor` | `Components\Identity` |
