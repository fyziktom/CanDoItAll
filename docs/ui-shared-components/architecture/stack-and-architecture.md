# Stack And Architecture

## Source and packaging

| Concern | Current implementation |
| --- | --- |
| Source root | `C:\repositories\CanDoItAll\src\CanDoItAll.Components` |
| Component files | `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components` |
| Assembly name | `CanDoItAll.Components` |
| SDK | `Microsoft.NET.Sdk.Razor` |
| Target framework | `net10.0` |
| Root namespace | `CanDoItAll.Components` |
| Component namespace | `CanDoItAll.Components` |
| Helper namespace | `CanDoItAll.Components` |

## NuGet and framework dependencies

The component project currently references:

- `Microsoft.AspNetCore.Components.Web`

## What this library really is

The project defines:

- shared enums and helpers in `ComponentPrimitives.cs`
- a tiny service layer for notifications
- helper extensions for merging `class` and `style` into `AdditionalAttributes`
- custom Razor components in the `CanDoItAll.Components` namespace

That gives host projects a familiar `<Button />`, `<DataGrid />`, `<Tabs />`, `<Stack />`, and similar syntax while keeping the runtime dependency surface very small.

## Consumption pattern

Current integration points:

- service registration via `services.AddCanDoItAllComponents()`
- namespace imports via `@using CanDoItAll.Components`
- CSS loading via `_content/CanDoItAll.Components.BaseLib/css/output.css`

## Internal design patterns

### 1. Thin wrapper components

Most components are one of these:

- direct wrappers over native HTML elements
- simple layout containers
- small parent-child registries implemented through `CascadingValue`

Examples:

- `TextBox` wraps `<input type="text">`
- `Tabs` registers `TabsItem` children
- `DataGrid` registers `DataGridColumn<TItem>` children
- `Chart` registers `LineSeries` and an optional `ValueAxis`

### 2. Attribute merging instead of bespoke styling APIs

Most components expose:

- `AdditionalAttributes`

The helper extensions merge incoming `class` and `style` with the component's base CSS. This is the main extension mechanism. There is very little component-level theming logic beyond this.

### 3. Utility-class-first styling

Components hardcode utility-like class strings such as:

- `rounded-xl border border-slate-200 bg-white p-4 shadow-sm`
- `inline-flex items-center justify-center gap-2 rounded-md border`

That means:

- the host app must load the generated stylesheet
- the component library is visually opinionated already
- component customization usually happens through extra classes, not new parameters

### 4. Partial API compatibility

Many components expose migration-friendly parameters, but only part of the contract is implemented.

Examples:

- `Alert.Variant` and `Alert.Shade` exist but do not change rendering
- `Button.Variant` only affects `Variant.Text`; `Shade` is ignored
- `LineSeries.CategoryProperty` and `Smooth` exist but are ignored in rendering
- `GridLines.Visible` exists but does not affect `Chart`

## Parent-child registration pattern

Several components use a hidden `CascadingValue` wrapper to register children:

- `DataGrid` <-> `DataGridColumn<TItem>`
- `Tabs` <-> `TabsItem`
- `Steps` <-> `StepsItem`
- `Chart` <-> `LineSeries`, `ValueAxis`

This matters for Codex because:

- child components often render no visible HTML by themselves
- behavior depends on placement inside the correct parent
- moving a child component outside the parent silently breaks it

## Service model

`AddCanDoItAllComponents()` currently registers only one service:

- `NotificationService`

There is no dialog service, tooltip service, context menu service, or theme service.

## Architecture constraints

The current library is intentionally small, but that creates real limitations:

- no form validation wrappers
- no modal/dialog engine
- no overlay positioning system
- no searchable/autocomplete select
- no real chart axes, labels, or series types
- no data-grid sorting/filtering/grouping
- no JS interop layer

## Practical architectural guidance

Use this library when you need:

- simple page layout
- utility-styled controls
- light workflow primitives
- small tables
- lightweight toast notifications

Do not stretch it into:

- enterprise grid behavior
- advanced form framework behavior
- full overlay system
- advanced data visualization

When an app needs those features, either extend the shared library deliberately or build a dedicated component instead of assuming the current surface already supports it.
