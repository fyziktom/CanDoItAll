# CanDoItAll.Components.Sandbox

## Purpose

Blazor sandbox for previewing, tuning, and regression-checking shared component behavior.

## Project Type

- SDK: `Microsoft.NET.Sdk.Web`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj
```

## References

Project references:

- `../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj`
- `../CanDoItAll.Components.Common/CanDoItAll.Components.Common.csproj`

Framework references:

- None

Direct package references:

- None

## Architecture Notes

Keep shared UI reusable and typed. Use BaseLib for ordinary product UI, CanvasLib for graph/canvas surfaces, OverlayLib for floating windows, WebGlLib for WebGL concepts, and sandbox projects only for demos or proof.

## Overlay Service Examples

The `/groups/overlays` route mounts and exercises BaseLib overlay services:

- `DialogService` examples cover compact, wide, full, backdrop-locked, and returned-object dialogs.
- `TooltipService` examples prove host-mounted tooltip rendering from a local trigger with configurable placement.
- `NotificationService` examples show service-triggered toasts through the shared layout host, including non-default positions.

The sandbox layout mounts `<DialogHost />`, `<Tooltip />`, and `<Notification />` once so pages can focus on service calls instead of overlay plumbing.

Use the overlay examples as the visual proof target for position changes. Notifications should be checked at the positions that matter to the consuming workflow, especially `TopRight`, `TopCenter`, `BottomCenter`, and any side-aligned stack used near list/detail or rail layouts. Tooltip checks should include the requested `TooltipPosition` value and at least one constrained viewport when using corner or edge placements such as `TopLeft`, `BottomRight`, `LeftTop`, or `RightBottom`.

The sandbox examples should stay aligned with the Components MCP guidance: choose positions that avoid covering primary controls, keep notification alerts on the compact X close control, and use Playwright snapshots or locator checks for non-default overlay placement.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
