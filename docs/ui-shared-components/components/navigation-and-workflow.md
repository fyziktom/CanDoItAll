# Navigation And Workflow

## Workflow components

| Component | Use it for | Key parameters | Important notes |
| --- | --- | --- | --- |
| `Tabs` | Tabbed content sections | `SelectedIndex`, `SelectedIndexChanged`, `RenderMode`, `TabItems`, `ChildContent`, `AdditionalAttributes` | Parent-child registration required |
| `TabsItem` | One tab page | `Text`, `Icon`, `ChildContent` | Must live inside `Tabs` |
| `Steps` | Stepper / onboarding flow | `Value`, `ValueChanged`, `Change`, `CanChange`, `CanSelect`, `ChildContent`, `AdditionalAttributes` | Good for small wizard flows |
| `StepsItem` | One step definition | `Text`, `Icon`, `Disabled`, `ChildContent` | Must live inside `Steps` |

## `Tabs` behavior

### `RenderMode.Server`

Behavior:

- renders only the active tab panel
- safer for heavier content
- inactive tabs are not present in the DOM

### `RenderMode.Client`

Behavior:

- renders all tab panels
- toggles inactive panels with `hidden`
- better when state should stay mounted client-side

## `Steps` behavior

`Steps` renders:

- a horizontal pill-style step list
- the active step’s `ChildContent` inside a bordered panel below

Selection rules:

- `Value` is clamped to existing step count
- clicking the current step does nothing
- disabled items cannot be selected
- if `CanSelect` is `false`, the stepper becomes display-only
- if `CanChange` is supplied, it must return `true` before the step changes

## Recommended usage patterns

### Tabs

```razor
<Tabs @bind-SelectedIndex="selectedTabIndex" RenderMode="TabRenderMode.Client">
    <TabItems>
        <TabsItem Text="My Scores">
            ...
        </TabsItem>
        <TabsItem Text="Published">
            ...
        </TabsItem>
        <TabsItem Text="Favorites">
            ...
        </TabsItem>
    </TabItems>
</Tabs>
```

### Steps

```razor
<Steps Value="@CurrentStep" CanSelect="false" class="panel-card">
    <StepsItem Text="Goal" />
    <StepsItem Text="Level" />
    <StepsItem Text="MIDI" />
</Steps>
```

## Limitations

### `Tabs`

- no disabled tabs
- no close buttons
- no lazy-loading callback
- no vertical mode
- no ARIA keyboard navigation beyond button click defaults

### `Steps`

- fixed visual layout
- no vertical mode
- no separate header/body templating
- no step-status icon override beyond the optional icon token

## Codex rules

- Use `Tabs` when the app already has 2-5 compact views on the same page.
- Use `RenderMode.Client` only when preserving inactive tab DOM matters.
- Use `Steps` for simple onboarding or setup flows, not for deep multistep forms with validation summaries or route-backed steps.
