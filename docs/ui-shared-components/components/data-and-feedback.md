# Data And Feedback

## Data display and feedback components

| Component | Use it for | Key parameters | Important notes |
| --- | --- | --- | --- |
| `DataGrid<TItem>` | Simple table rendering | `Data`, `RowSelect`, `AllowPaging`, `PageSize`, `Columns`, `ChildContent`, `AdditionalAttributes` | Minimal implementation only |
| `DataGridColumn<TItem>` | Column definition | `Property`, `Title`, `Template` | Uses reflection for `Property` |
| `Chart` | Lightweight SVG line chart | `ChildContent`, `AdditionalAttributes` | Supports only registered `LineSeries` plus optional `ValueAxis` |
| `LineSeries` | One line in the chart | `Data`, `CategoryProperty`, `ValueProperty`, `Title`, `Smooth` | `CategoryProperty` and `Smooth` are currently ignored |
| `CategoryAxis` | Compatibility child node | `ChildContent` | Placeholder only |
| `ValueAxis` | Optional min/max override | `Min`, `Max`, `ChildContent` | Only min/max affect rendering |
| `GridLines` | Compatibility child node | `Visible` | Placeholder only |
| `Alert` | Inline status message | `AlertStyle`, `Variant`, `Shade`, `ChildContent`, `AdditionalAttributes` | `Variant` and `Shade` do not currently affect CSS |
| `Notification` | Toast container | none | Requires `NotificationService` and one instance in layout |
| `ProgressBar` | Simple progress fill | `Value`, `Min`, `Max`, `Unit`, `AdditionalAttributes` | `Unit` is declared but unused |
| `Dialog` | Placeholder host | none | No real dialog behavior |
| `Tooltip` | Placeholder host | none | No real tooltip behavior |
| `ContextMenu` | Placeholder host | none | No real context menu behavior |

## `DataGrid<TItem>` details

Implemented:

- static column definitions
- row click callback
- simple paging
- reflection-based property reading
- per-column custom templates

Not implemented:

- sorting
- filtering
- grouping
- selection state
- virtualization
- empty template
- responsive column collapse
- column resizing/reordering

Important note:

The app sometimes passes extra attributes such as `Responsive="true"`. Those values are simply forwarded to the root div and do not create special behavior.

### Typical usage

```razor
<DataGrid Data="@recentAttempts"
          TItem="PracticeAttemptRecord"
          AllowPaging="true"
          PageSize="12">
    <Columns>
        <DataGridColumn TItem="PracticeAttemptRecord"
                        Property="@nameof(PracticeAttemptRecord.Mode)"
                        Title="Mode" />
        <DataGridColumn TItem="PracticeAttemptRecord" Title="Correct">
            <Template Context="item">
                @(item.Correct ? "Yes" : "No")
            </Template>
        </DataGridColumn>
    </Columns>
</DataGrid>
```

## `Chart` details

The current chart implementation:

- renders SVG manually
- uses a fixed `viewBox="0 0 1000 280"`
- always draws 5 horizontal grid lines
- plots one or more polylines
- creates a legend from series titles

Actual supported behavior:

- `ValueAxis.Min` and `ValueAxis.Max` can clamp the numeric domain
- each `LineSeries` reads numeric values via `ValueProperty`
- each series title shows in the legend

Declared but currently inert:

- `CategoryAxis`
- `GridLines.Visible`
- `LineSeries.CategoryProperty`
- `LineSeries.Smooth`

### Practical implication

Use `Chart` only for:

- small sparkline-style trend views
- quick internal dashboards

Do not use it when you need:

- labeled axes
- tooltips
- hover states
- bar/pie/area charts
- multiple axis types
- responsive label strategies

## `Alert`

Useful for:

- inline success/warning/error info
- simple empty-state or auth messages

Current style map:

- `Success`
- `Warning`
- `Danger`
- `Light`
- `Secondary`
- everything else falls back to info styling

## `Notification`

`Notification` is the real feedback component in this group.

Requirements:

- one `<Notification />` instance in a shared layout
- `NotificationService` registered in DI

Usage:

```csharp
NotificationService.Notify(new NotificationMessage
{
    Severity = NotificationSeverity.Success,
    Summary = "Saved",
    Detail = "Clef coefficients persisted.",
    Duration = 2500
});
```

Current limitations:

- fixed top-right placement
- no action buttons
- no manual dismiss button
- no stacking limit
- no animation control

## Placeholder hosts

These components exist for compatibility only:

- `Dialog`
- `Tooltip`
- `ContextMenu`

They currently render hidden host divs and nothing more. They are not backed by services, JS interop, or event APIs.

## Codex rules

- Use `NotificationService` for lightweight toasts.
- Use `Alert` for inline message blocks.
- Use `DataGrid` only for small, simple tables.
- Treat `Chart` as a stopgap component, not a charting platform.
- Do not build new features on top of `Dialog`, `Tooltip`, or `ContextMenu` without implementing them first.
