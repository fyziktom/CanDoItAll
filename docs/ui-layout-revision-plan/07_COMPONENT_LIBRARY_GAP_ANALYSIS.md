# Component Library Gap Analysis

## Current Shared UI Layers

### `CanDoItAll.Components`

Current role:

- low-level compatibility primitives
- simple layout/input wrappers
- intentionally narrow behavior

Notable realities confirmed in code and repo docs:

- `Button.razor` exists, but the route pages do not use it
- `FormField.razor` is visual-only and unused by route pages
- `Dialog.razor` is a hidden placeholder host
- `ContextMenu.razor` is a hidden placeholder host
- `DataGrid.razor` is intentionally minimal

Conclusion:

- this library is not enough to standardize the application's page composition by itself

### `CanDoItAll.ComponentKit`

Current role:

- shell/workbench composites
- newer, more application-shaped UI pieces

Useful existing assets:

- `AppShell`
- `AppTabStrip`
- `PageHeader`
- `SectionCard`
- `StatusBadge`
- `CanvasWorkbench`
- `CanvasWorkbenchStage`

Conclusion:

- phase-1 page-composition additions should land here, not in the lower-level library

## Main Standardization Direction

Use `CanDoItAll.ComponentKit` as the owner of:

- page scaffolds
- page shell modes
- list/detail composition
- form sectioning
- standardized empty/loading/action regions

Keep `CanDoItAll.Components` as the lower-level primitive layer until there is an explicit future plan to consolidate the two libraries.

## Missing Components Causing Page Improvisation

### 1. `PageScaffold`

Why needed:

- pages share header-plus-body structure but repeat spacing and width decisions

Pages that benefit:

- all standard route pages

Should live in:

- `CanDoItAll.ComponentKit`

High-level API shape:

- `Mode`: `Standard`, `FocusWorkbench`
- `Header`
- `Body`
- optional `SecondaryRail`
- optional `MaxWidth`

Rules it should standardize:

- one clear page entry point
- consistent vertical spacing
- route-aware width and rail behavior

### 2. `ListDetailShell`

Why needed:

- Projects, Resources, Prompt Gallery, Validation, Test Lab, and Settings all use some version of list-plus-editor

Pages that benefit:

- Projects
- Resources
- Prompt Gallery
- Validation Center
- Test Lab
- Settings

Should live in:

- `CanDoItAll.ComponentKit`

High-level API shape:

- `ListHeader`
- `ListContent`
- `DetailHeader`
- `DetailContent`
- `SelectedItemKey`
- responsive split ratio

Rules it should standardize:

- selected state
- list/detail spacing
- responsive collapse
- consistent section order

### 3. `ListPanelHeader`

Why needed:

- list panes currently mix counts, buttons, and optional filters ad hoc

Pages that benefit:

- Projects
- Resources
- Prompt Gallery
- Validation Center
- Test Lab
- Settings
- Automation

Should live in:

- `CanDoItAll.ComponentKit`

High-level API shape:

- `Title`
- `Count`
- `PrimaryAction`
- `SecondaryActions`
- `Filters`

Rules it should standardize:

- count placement
- action placement
- relation between search/filter and list content

### 4. `FilterBar`

Why needed:

- several management pages need light filtering but currently expose none

Pages that benefit:

- Projects
- Resources
- Prompt Gallery
- Validation Center
- Test Lab
- Automation

Should live in:

- `CanDoItAll.ComponentKit`

High-level API shape:

- `SearchSlot`
- `FilterSlots`
- `ResultCount`
- `ResetAction`

Rules it should standardize:

- horizontal grouping
- wrap behavior
- empty/no-results relationship

### 5. `SelectionListItem`

Why needed:

- list rows are currently raw buttons with no shared selected state treatment

Pages that benefit:

- Projects
- Resources
- Prompt Gallery
- Validation Center
- Test Lab
- Settings

Should live in:

- `CanDoItAll.ComponentKit`

High-level API shape:

- `IsSelected`
- `Title`
- `Meta`
- `Summary`
- optional `Status`
- optional `Actions`

Rules it should standardize:

- selected visuals
- hover/active states
- alignment of status and metadata

### 6. `FormSection`

Why needed:

- long editors currently read as one long stack of controls

Pages that benefit:

- Projects
- Resources
- Prompt Gallery
- Validation Center
- Test Lab
- Settings

Should live in:

- `CanDoItAll.ComponentKit`

High-level API shape:

- `Title`
- `Description`
- `ChildContent`
- optional `Actions`

Rules it should standardize:

- section spacing
- subsection header style
- grouping rhythm inside long forms

### 7. `StickyActionFooter`

Why needed:

- save/reset/delete actions on long pages are easy to lose

Pages that benefit:

- Projects
- Resources
- Validation Center
- Test Lab
- Settings

Should live in:

- `CanDoItAll.ComponentKit`

High-level API shape:

- `PrimaryAction`
- `SecondaryActions`
- `StatusMessage`
- `IsDirty`

Rules it should standardize:

- save/cancel/delete placement
- destructive separation
- sticky behavior on long editors

### 8. `EmptyState` And `LoadingState`

Why needed:

- state handling is inconsistent and often too weak

Pages that benefit:

- Home
- Projects
- Resources
- Prompt Gallery
- Validation Center
- Test Lab
- Activity
- Automation
- Project Calendar

Should live in:

- `CanDoItAll.ComponentKit`

High-level API shape:

- `Title`
- `Description`
- optional `PrimaryAction`
- optional `SecondaryAction`
- optional `Icon`

Rules it should standardize:

- difference between empty, no results, and loading
- next-step guidance
- spacing and tone

### 9. `SummaryTiles`

Why needed:

- pages repeatedly need small metric summaries but currently improvise them

Pages that benefit:

- Dashboard
- Validation Center
- Test Lab
- Automation
- Project Calendar

Should live in:

- `CanDoItAll.ComponentKit`

High-level API shape:

- collection of labeled metrics with tone and optional helper text

Rules it should standardize:

- tile spacing
- numeric emphasis
- meaning of tone

### 10. `KeyValueBlock`

Why needed:

- pages need compact metadata presentation without inventing card markup repeatedly

Pages that benefit:

- Resources
- Project Calendar
- Validation Center
- Automation
- Settings

Should live in:

- `CanDoItAll.ComponentKit`

High-level API shape:

- list of label/value rows
- optional density mode

Rules it should standardize:

- label/value alignment
- metadata readability

### 11. `SecondaryTabs`

Why needed:

- some pages need local task switching, not another full card stack

Pages that benefit:

- Prompt Gallery
- Settings
- Test Lab

Should live in:

- `CanDoItAll.ComponentKit`

High-level API shape:

- tab items
- selected key
- header slot

Rules it should standardize:

- local navigation within a page
- distinction between peers and subsections

### 12. `ContextHint`

Why needed:

- Resources and similar pages need a reusable, non-noisy information block

Pages that benefit:

- Resources
- Validation Center
- Settings
- Home

Should live in:

- `CanDoItAll.ComponentKit`

High-level API shape:

- `Title`
- `Body`
- optional tone

Rules it should standardize:

- informative guidance without looking like an alert

### 13. `StatusChipSet`

Why needed:

- the app currently mixes `StatusBadge` with raw status pills and inline spans

Pages that benefit:

- shell
- Prompt Gallery
- Resources
- Validation Center
- Settings
- Project Calendar
- Automation

Should live in:

- `CanDoItAll.ComponentKit`

High-level API shape:

- small chip collection
- tone mapping
- optional icon

Rules it should standardize:

- success/warning/info/neutral semantics
- size and wrapping behavior

### 14. `FocusWorkbenchShellMode`

Why needed:

- protected routes need a different shell behavior, not a different inner page implementation

Pages that benefit:

- Project Structure
- Prompt Factory
- possibly Project Calendar

Should live in:

- `CanDoItAll.ComponentKit` plus `MainLayout` route logic

High-level API shape:

- shell mode enum or route policy
- toggles for compact top bar, right rail visibility, max width

Rules it should standardize:

- quieter workbench framing
- reduced duplicate chrome
- no interference with protected workbench internals

## Components That Should Not Be Expanded In Phase 1 Unless Required

- `Dialog`
- `ContextMenu`
- `Tooltip`
- `DataGrid`

Reason:

- phase 1 does not need a full overlay or advanced grid program to fix the current layout inconsistency
- those changes would add scope quickly

## Reusability Priority

### Highest priority

- `PageScaffold`
- `ListDetailShell`
- `ListPanelHeader`
- `FormSection`
- `StickyActionFooter`
- `EmptyState`
- `FocusWorkbenchShellMode`

### Medium priority

- `FilterBar`
- `SelectionListItem`
- `SummaryTiles`
- `KeyValueBlock`
- `SecondaryTabs`
- `ContextHint`

### Lower priority for phase 1

- shared action menu
- advanced dialog confirmation system
- advanced grid work

## Recommended Standardization Strategy

1. do not force current pages onto the older low-level button/card abstractions
2. introduce page-composition primitives in `CanDoItAll.ComponentKit`
3. upgrade `PageHeader` usage instead of replacing it
4. migrate high-value pages onto the new patterns
5. only then decide whether older low-level primitives should be re-skinned or consolidated
