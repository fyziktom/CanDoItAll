# CanDoItAll.Components.BaseLib

## Purpose

Primary shared Razor component library with theme tokens, layout primitives, forms, buttons, cards, feedback, navigation, tabs, and CSS output.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj
```

## References

Project references:

- `../CanDoItAll.Components.Common/CanDoItAll.Components.Common.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.AspNetCore.Components.Web (10.0.4)`

## Architecture Notes

Keep shared UI reusable and typed. Use BaseLib for ordinary product UI, CanvasLib for graph/canvas surfaces, OverlayLib for floating windows, WebGlLib for WebGL concepts, and sandbox projects only for demos or proof.

## Overlay Services

BaseLib provides scoped services for app-level overlays:

- `DialogService` opens service-driven dialogs through a mounted `<DialogHost />`.
- `TooltipService` opens pointer-positioned tooltips through a mounted `<Tooltip />`.
- `NotificationService` owns toast messages rendered by a mounted `<Notification />`.

Register the services once:

```csharp
builder.Services.AddCanDoItAllBaseLib();
```

Mount the hosts once in the interactive layout:

```razor
<DialogHost />
<Tooltip />
<Notification />
```

`DialogService.OpenAsync(...)` returns the object supplied to `DialogReference.CloseAsync(result)`. Existing direct `<Dialog IsOpen="...">` usage remains supported for controlled component flows.

Notifications can be positioned per message with `NotificationMessage.Position` or the `Notify(..., position: ...)` overload. Supported positions cover top, center, and bottom stacks on the left, center, and right edges, with `TopRight` as the default.

Tooltips can be positioned with `TooltipOptions.Position` or `TooltipTarget Position`. The enum supports the standard `Top`, `Bottom`, `Left`, and `Right` placements plus corner and edge alignments such as `TopLeft`, `BottomRight`, `LeftTop`, and `RightBottom`.

### Overlay Positioning Guidance

Notification placement should protect the current workflow first. Use `TopRight` for ordinary desktop toasts, `BottomCenter` when top navigation or mobile reach would make top stacks awkward, and side positions such as `BottomLeft`, `CenterLeft`, or `CenterRight` when the message belongs near a side rail, list pane, or action region. Reserve `TopCenter` for global high-importance messages that still do not require a decision. If the user must choose, confirm, or resolve something before continuing, use `DialogService` or an inline `Alert` instead of a notification.

Notification alerts intentionally use only the compact X close control so the message width stays available for useful summary and detail text. Keep copy short, set `Duration` deliberately, and use per-message `Position` when one notification needs to appear somewhere other than the host default.

Tooltip placement should keep the bubble visible and away from the next likely action. Use `Top` or `Right` when there is room, `Bottom` for triggers near the top edge, `Top` for triggers near lower toolbars or footers, and `Left` or `Right` for dense inline controls. Use corner or edge placements such as `TopLeft`, `BottomRight`, `LeftTop`, or `RightBottom` near viewport, card, toolbar, or panel corners so the tooltip does not cover neighboring controls.

For agent-driven changes, query the Components MCP metadata for `Notification`, `Tooltip`, or `TooltipTarget` before choosing non-default positions, then validate unusual placements in the sandbox with Playwright at the viewport sizes used by the target page.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
