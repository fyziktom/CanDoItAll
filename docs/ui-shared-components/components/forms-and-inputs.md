# Forms And Inputs

## Input and form components

| Component | Use it for | Key parameters | Important notes |
| --- | --- | --- | --- |
| `Button` | Primary action buttons | `Text`, `Icon`, `ButtonStyle`, `Size`, `Variant`, `Disabled`, `IsBusy`, `Click`, `AdditionalAttributes` | `Variant.Text` is implemented; `Shade` is ignored; `Dark` and `Base` styles fall back to primary |
| `FormField` | Label + content + prefix/suffix wrapper | `Text`, `ChildContent`, `Start`, `End`, `AdditionalAttributes` | Good default wrapper for single-field layouts |
| `Fieldset` | Titled grouped inputs | `AllowCollapse`, `HeaderTemplate`, `ChildContent` | Collapse mode uses native `<details>` and always renders `open`; expand/collapse titles are mostly unused |
| `TextBox` | Single-line text input | `Value`, `ValueChanged`, `Change`, `Placeholder`, `ReadOnly`, `AdditionalAttributes` | Updates on `change`, not `input` |
| `TextArea` | Multi-line text input | `Value`, `ValueChanged`, `Change`, `Placeholder`, `ReadOnly`, `Rows`, `AdditionalAttributes` | Updates on `change`, not live typing |
| `Password` | Password input | `Value`, `ValueChanged`, `Change`, `Placeholder`, `AdditionalAttributes` | Same update model as `TextBox` |
| `Numeric<TValue>` | Numeric entry | `Value`, `ValueChanged`, `Change`, `Min`, `Max`, `Step`, `Format`, `Disabled`, `AdditionalAttributes` | Parses on `change`; limited numeric conversion support |
| `Slider<TValue>` | Range selection | `Value`, `ValueChanged`, `Change`, `Min`, `Max`, `Step`, `AdditionalAttributes` | Parses numeric values and clamps to range |
| `DropDown<TValue>` | Simple select list | `Data`, `Value`, `ValueChanged`, `Change`, `TextProperty`, `ValueProperty`, `Placeholder`, `AllowClear`, `Disabled`, `AdditionalAttributes` | Reflection-based, single select only, no templates or search |
| `CheckBox<TValue>` | Bool / nullable-bool checkbox | `Value`, `ValueChanged`, `Change`, `Name`, `AdditionalAttributes` | Realistic only for `bool` and `bool?` |
| `Switch` | Bool toggle switch | `Value`, `ValueChanged`, `Change`, `Disabled`, `AdditionalAttributes` | Visually richer than `CheckBox`, but bool-only |

## Binding guidance

### Components that are safe with `@bind-Value`

- `TextBox`
- `TextArea`
- `Password`
- `Numeric<TValue>`
- `Slider<TValue>`
- `DropDown<TValue>`
- `CheckBox<TValue>`
- `Switch`

### Important behavior difference

Text inputs use `@onchange`, not `@oninput`.

That means:

- the bound value updates on blur or Enter
- live-search text boxes will feel delayed unless you build a custom input
- validation or derived UI that depends on immediate keystrokes should use a different component

## Recommended usage patterns

### Basic account form

```razor
<Row Gap="1rem">
    <Column Size="12" SizeMD="6">
        <TextBlock TextStyle="TextStyle.H6">Login</TextBlock>
        <Stack Gap="0.6rem" style="margin-top:0.5rem">
            <TextBox Placeholder="Email" @bind-Value="email" />
            <Password Placeholder="Password" @bind-Value="password" />
            <Button Text="Login" Icon="login" Click="LoginAsync" />
        </Stack>
    </Column>
</Row>
```

### Structured numeric settings

```razor
<FormField Text="Staff Zoom">
    <ChildContent>
        <Numeric TValue="double"
                 @bind-Value="settings.StaffZoom"
                 Min="@0.5m"
                 Max="@2.0m"
                 Step="0.05"
                 style="width:100%" />
    </ChildContent>
</FormField>
```

### Reflection-based dropdown

```razor
<DropDown TValue="PracticeSessionMode?"
          Data="@modeFilters"
          TextProperty="@nameof(ModeFilterItem.Label)"
          ValueProperty="@nameof(ModeFilterItem.Value)"
          @bind-Value="selectedMode" />
```

## Behavioral limitations

### `Button`

- `IsBusy` disables clicking and shows a spinner.
- There is no `ButtonType` parameter, but you can still pass `type="submit"` through `AdditionalAttributes`.
- `Variant.Text` is the only variant that materially changes styling.

### `DropDown<TValue>`

- Internally normalizes options by index, not by stable value key.
- Rebuilding `Data` changes option keys.
- No support for:
  - item templates
  - groups
  - disabled items
  - search/filtering
  - multi-select
  - async loading UI

### `Numeric<TValue>`

Supported conversions are effectively:

- `int`
- `long`
- `float`
- `double`
- `decimal`
- nullable versions of those

### `Fieldset`

Important limitations:

- `AllowCollapse` switches to native `<details>`
- `ExpandAriaLabel` and `ExpandTitle` are declared but not used
- no state callback is exposed

## Codex rules

- Use `FormField` for consistency when the page already follows the shared component visual style.
- Avoid `TextBox` and `TextArea` for live-search or typeahead experiences.
- Use `DropDown` only for small, known lists.
- If the UX needs autocomplete, async options, tags, or command-menu behavior, build a new shared component instead of stretching `DropDown`.
