# Codex Usage Guide

This guide is the operational checklist Codex should use before picking a shared UI component from the current `CanDoItAll.Components` layer.

## 1. First decide whether the shared library is appropriate

Use the shared library when the screen needs:

- cards, layout stacks, grid columns
- simple text input and numeric input
- tabs or steps
- compact data tables
- lightweight trend charts
- inline alerts or toast notifications

Do not use the shared library as-is when the screen needs:

- modal dialogs
- true tooltips
- advanced context menus
- searchable selects or autocomplete
- complex validation UX
- rich data grids
- production-grade charting

## 2. Bootstrap checklist for any new host project

### Services

```csharp
services.AddCanDoItAllComponents();
```

### Imports

```razor
@using CanDoItAll.Components
```

### CSS

Load:

- `_content/CanDoItAll.Components/css/output.css`

Without that CSS, component markup still renders, but the intended visual system is incomplete.

## 3. Component-selection quick map

| Need | Preferred component | Avoid when |
| --- | --- | --- |
| Page or card actions | `Button` | You need submit semantics plus richer states than `disabled` or `busy` |
| Horizontal or vertical arrangement | `Stack` | You need grid math or breakpoint-specific columns |
| Responsive section layout | `Row` + `Column` | You need complex nested CSS grid behavior |
| Field label + control | `FormField` | You need validation summary or field-level error rendering |
| Text input | `TextBox` | You need live search or `oninput` updates |
| Numeric input | `Numeric<TValue>` | You need culture-specific formatting or spinbutton UX |
| Select | `DropDown<TValue>` | You need search, templates, async results, or multi-select |
| Toggle | `Switch` | You need tri-state or checkbox semantics |
| Boolean checkbox | `CheckBox<bool>` | You need anything other than bool or bool? |
| Tabbed sections | `Tabs` | You need disabled tabs, close buttons, or route-backed tabs |
| Wizard steps | `Steps` | You need complex validation per step |
| Small data table | `DataGrid<TItem>` | You need sorting, filtering, selection, or virtualization |
| Trend visualization | `Chart` + `LineSeries` | You need robust charting |
| Inline messages | `Alert` | You need dismissible message bars |
| Toast | `NotificationService` + `Notification` | You need actions or persistent toasts |

## 4. Rules about assumptions

### Assume partial implementation unless documented otherwise

Bad assumption:

- "This looks feature-complete, so `Variant.Outlined` will work everywhere."

Safer assumption:

- "This library copied only the API shape it needed. I must verify the component implementation."

### Check for hidden placeholders

If the component name is:

- `Dialog`
- `Tooltip`
- `ContextMenu`

Treat it as unimplemented.

### Check input event timing

For `TextBox`, `TextArea`, and `Password`:

- updates happen on `change`
- not on every keystroke

That affects:

- search
- live validation
- computed previews

## 5. Composition guidance

### Preferred pattern

- `Card`
- `Stack`
- `FormField`
- simple control components
- `Button`

This matches the existing library's usage and is low risk.

### Preferred responsive pattern

- `Row Gap="..."`
- `Column Size="12" SizeMD="..." SizeXL="..."`

### Preferred workflow pattern

- `Tabs` for side-by-side domains
- `Steps` for single linear flow

## 6. Known footguns

- `Sidebar` has `ExpandedChanged` but never raises it.
- `Alert.Variant` and `Alert.Shade` are inert.
- `Button.Shade` is inert.
- `DataGrid` forwards unknown attributes but does not implement responsive behavior.
- `Chart` ignores `CategoryAxis`, `GridLines.Visible`, and `LineSeries.Smooth`.
- `ProgressBar.Unit` is unused.

## 7. When to create a new shared component

Create a new shared component instead of overloading an old one when:

- the new UX needs real behavior, not styling only
- at least two pages will need the same richer interaction
- the current component would require hidden conventions or one-off hacks

Examples that justify new shared components:

- searchable combo box
- modal dialog service
- autocomplete or tag picker
- validation-aware field wrapper
- real chart wrapper
- file upload card

## 8. Safe implementation strategy for Codex

When building a new page:

1. Use the shared component docs in this folder first.
2. Reuse existing components only for their implemented surface.
3. If the requirement exceeds the documented surface, create or propose a new shared component explicitly.
4. Avoid writing page-specific hacks that pretend a stub component is functional.
