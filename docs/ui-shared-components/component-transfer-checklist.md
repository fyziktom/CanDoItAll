# Component Transfer Checklist

This checklist tracks the component-library copy currently stored in `CanDoItAll`.

## Components

- [x] Alert
- [x] Body
- [x] Button
- [x] Card
- [x] CategoryAxis
- [x] Chart
- [x] CheckBox
- [x] Column
- [x] ContextMenu
- [x] DataGrid
- [x] DataGridColumn
- [x] Dialog
- [x] DropDown
- [x] Fieldset
- [x] FormField
- [x] GridLines
- [x] Header
- [x] Icon
- [x] Layout
- [x] LineSeries
- [x] Notification
- [x] Numeric
- [x] Password
- [x] ProgressBar
- [x] Row
- [x] Sidebar
- [x] Slider
- [x] Stack
- [x] Steps
- [x] StepsItem
- [x] Switch
- [x] Tabs
- [x] TabsItem
- [x] TextArea
- [x] TextBlock
- [x] TextBox
- [x] Tooltip
- [x] ValueAxis

## Support Types

- [x] ComponentNamespaceMarker
- [x] ComponentPrimitives
- [x] DropDownOption
- [x] FontAwesomeIconCatalog
- [x] Component `_Imports`

## Styling Assets

- [x] Shared Tailwind workspace
- [x] Generated component stylesheet

## Verification

- [x] Source inventory counted: 41 files total in the legacy component folder (38 components, 2 support types, 1 `_Imports`)
- [x] Destination inventory matched: 41 transferred files total (40 files in `Components`, 1 root `_Imports.razor`)
- [x] `dotnet build CanDoItAll.slnx` completed successfully
- [x] `npm.cmd run build` completed successfully in `Tailwind`
