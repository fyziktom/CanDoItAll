# Layout And Typography

## Layout containers

| Component | Use it for | Key parameters | Important notes |
| --- | --- | --- | --- |
| `Layout` | App shell root | `ChildContent`, `AdditionalAttributes` | Adds `rz-layout min-h-dvh` and nothing else |
| `Header` | Top header wrapper | `ChildContent`, `AdditionalAttributes` | Semantic `<header>` only |
| `Body` | Main body wrapper | `ChildContent`, `AdditionalAttributes` | Semantic `<main>` only |
| `Sidebar` | Collapsible side panel wrapper | `Expanded`, `ExpandedChanged`, `ChildContent`, `AdditionalAttributes` | `Expanded` only toggles `hidden`; `ExpandedChanged` is declared but never invoked internally |
| `Card` | Standard panel container | `ChildContent`, `AdditionalAttributes` | Opinionated white card with border/shadow |
| `Row` | 12-column grid container | `Gap`, `ChildContent`, `AdditionalAttributes` | Uses inline `display:grid` and `gap` style |
| `Column` | Responsive column slot | `Size`, `SizeMD`, `SizeXL`, `ChildContent`, `AdditionalAttributes` | Only 3 breakpoints exist; values are clamped to `1..12` |
| `Stack` | Flexbox helper | `Orientation`, `AlignItems`, `JustifyContent`, `Wrap`, `Gap`, `ChildContent`, `AdditionalAttributes` | Best choice for most toolbars, card actions, and small layouts |

## Typography and icon primitives

| Component | Use it for | Key parameters | Important notes |
| --- | --- | --- | --- |
| `TextBlock` | Shared text styles | `TextStyle`, `Value`, `Style`, `ChildContent`, `AdditionalAttributes` | Only `H4`, `H5`, `H6` render heading tags; all other styles render `<p>` |
| `Icon` | Shared icon token rendering | `Name`, `AdditionalAttributes` | Uses `FontAwesomeIconCatalog`; unknown tokens render text fallback |

## Recommended usage patterns

### App shell

```razor
<Layout class="app-layout">
    <Header class="app-header">
        <Stack Orientation="Orientation.Horizontal"
               AlignItems="AlignItems.Center"
               JustifyContent="JustifyContent.SpaceBetween"
               Gap="0.75rem">
            <TextBlock TextStyle="TextStyle.H6">Workspace</TextBlock>
            <Button Icon="menu" Click="ToggleSidebar" />
        </Stack>
    </Header>

    <Sidebar @bind-Expanded="sidebarOpen" class="app-sidebar">
        ...
    </Sidebar>

    <Body class="app-body">
        ...
    </Body>
</Layout>
```

### Responsive content area

```razor
<Row Gap="1rem">
    <Column Size="12" SizeMD="8">
        <Card>Primary content</Card>
    </Column>
    <Column Size="12" SizeMD="4">
        <Card>Secondary content</Card>
    </Column>
</Row>
```

### Horizontal action stack

```razor
<Stack Orientation="Orientation.Horizontal"
       Wrap="FlexWrap.Wrap"
       AlignItems="AlignItems.Center"
       Gap="0.5rem">
    <Button Text="Save" Icon="save" />
    <Button Text="Cancel" ButtonStyle="ButtonStyle.Light" />
</Stack>
```

## Behavior notes by component

### `Row`

- Always emits 12 grid columns.
- `Gap` is passed straight through as CSS, so values such as `1rem`, `12px`, and `0.75rem` work.

### `Column`

- Default width is full width (`12`).
- `SizeMD` falls back to `Size`.
- `SizeXL` falls back to `SizeMD`.
- There are no `SizeSM`, `SizeLG`, or offset parameters.

### `Stack`

- This is the most useful layout primitive in the library.
- It maps enums to direct CSS flex values.
- It is the safest alternative to hand-rolled flex wrappers.

### `Sidebar`

Important limitation:

- `@bind-Expanded` works only because the parent updates the value itself.
- The component does not call `ExpandedChanged`.
- Use a parent method like `sidebarOpen = !sidebarOpen;` rather than expecting sidebar-internal toggle behavior.

### `TextBlock`

Implemented style mapping:

- `H4`, `H5`, `H6`
- `Subtitle1`, `Subtitle2`
- `Body1`, `Body2`
- `Caption`

Not implemented:

- no `H1`, `H2`, `H3`
- no link variant
- no truncation or multiline clamping parameters

## Codex rules

- Prefer `Stack` before custom flex wrappers.
- Prefer `Row` and `Column` when you need page sections with stable visual rhythm.
- Use `TextBlock` for shared text tone, but use raw markup when semantic heading level matters beyond `H4-H6`.
- Do not rely on `Sidebar` to emit state changes on its own.
