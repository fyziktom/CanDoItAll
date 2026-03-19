# Helpers, Enums, And Models

## Core helper file

Most shared primitives live in:

- `C:\repositories\zyphonote\src\App.Components\Radzen\RadzenPrimitives.cs`

## Helper extensions

### `ComponentAttributeExtensions.WithClass(...)`

Purpose:

- merges the component’s base `class` with `AdditionalAttributes["class"]`

Why it matters:

- this is the standard extension point for nearly every component
- custom classes do not replace base classes, they append to them

### `ComponentAttributeExtensions.WithClassAndStyle(...)`

Purpose:

- merges both base class/style and incoming `class`/`style`

Why it matters:

- layout components such as `Row`, `Column`, `Stack`, and `TextBlock` rely on this to preserve generated inline layout styles

## Notification model

| Type | Purpose | Notes |
| --- | --- | --- |
| `NotificationSeverity` | `Info`, `Success`, `Warning`, `Error` | Used by `NotificationService` and `Notification` |
| `NotificationMessage` | Toast payload | `Summary`, `Detail`, `Duration`, `Severity` |
| `NotificationService` | Scoped event dispatcher | Only service registered by `AddRadzenComponents()` |

## Internal support models

| Type | Location | Purpose |
| --- | --- | --- |
| `DropDownOption<TValue>` | `DropDownOption.cs` | Internal normalized option record for `<DropDown>` |
| `FontAwesomeIconCatalog` | `FontAwesomeIconCatalog.cs` | Maps Material-style icon tokens to Font Awesome class names |

`DropDownOption<TValue>` is internal. Consumers never pass it directly.

## Enum surface

### Layout enums

| Enum | Members | Used by |
| --- | --- | --- |
| `Orientation` | `Horizontal`, `Vertical` | `Stack` |
| `AlignItems` | `Start`, `Center`, `End`, `Stretch` | `Stack` |
| `JustifyContent` | `Start`, `Center`, `End`, `SpaceBetween`, `SpaceAround` | `Stack` |
| `FlexWrap` | `NoWrap`, `Wrap` | `Stack` |

### Button and visual enums

| Enum | Members | Used by | Reality check |
| --- | --- | --- | --- |
| `ButtonStyle` | `Primary`, `Secondary`, `Success`, `Info`, `Warning`, `Danger`, `Light`, `Dark`, `Base` | `Button` | `Dark` and `Base` currently fall through to primary styling |
| `ButtonSize` | `Small`, `Medium`, `Large` | `Button` | Implemented |
| `Variant` | `Filled`, `Flat`, `Outlined`, `Text` | `Button`, `Alert` | Only `Button` handles `Text`; most other uses are inert |
| `Shade` | `Default`, `Lighter`, `Light`, `Dark`, `Darker` | `Button`, `Alert` | Currently inert in component rendering |
| `TextStyle` | `Body1`, `Body2`, `Caption`, `H4`, `H5`, `H6`, `Subtitle1`, `Subtitle2` | `TextBlock` | Implemented |
| `AlertStyle` | `Base`, `Primary`, `Secondary`, `Success`, `Info`, `Warning`, `Danger`, `Light`, `Dark` | `Alert` | Only a subset maps to distinct styles; `Base`, `Primary`, and `Dark` collapse to default info styling |

### Workflow enums

| Enum | Members | Used by |
| --- | --- | --- |
| `TabRenderMode` | `Server`, `Client` | `Tabs` |

## Icon mapping

`FontAwesomeIconCatalog` accepts:

- a Material-style token such as `menu`, `workspace_premium`, `library_music`
- or an already-formed Font Awesome class list such as `fa-solid fa-music`

If a token is unknown:

- `Icon` renders a fallback text token
- `Button`, `Tabs`, and `Steps` also fall back to token text

## DI helper

### `ServiceCollectionExtensions.AddRadzenComponents(...)`

Current behavior:

```csharp
services.AddScoped<NotificationService>();
```

Implication:

- adding the component library does not add JS, theme, or overlay infrastructure
- if future shared components need service-backed behavior, this method will become the central registration point

## Codex guidance

- If a parameter comes from these enums, verify in the component source that the enum member actually changes rendering.
- If a feature looks like classic Radzen behavior but there is no supporting service or JS here, assume it is not implemented.
