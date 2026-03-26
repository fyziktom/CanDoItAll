# Compatibility Notes

## Runtime Compatibility State

- No debug-only host exposure remains in the shared calendar runtime. The temporary `host.__debugCalendarController` hook used during diagnosis was removed before the final validation pass.
- Shared calendar ownership remains in CanDoItAll. The responsive week-view fixes were applied in `CanDoItAll.Components.CanvasLib` and mirrored into the legacy `CanDoItAll.ComponentKit` JS files so any remaining legacy consumers do not diverge during the migration window.
- Seller-profile-specific styling remains in Zyphonote. No seller-profile rules were moved into shared runtime libraries.

## Proof-Only Compatibility Handling

- `CaptureProof/Program.cs` temporarily replaces live `<canvas>` elements with PNG snapshots generated through `toDataURL()` immediately before screenshot capture.
- This exists because Playwright page screenshots in this workspace can intermittently blank portions of HTML canvas even when the page rendered correctly in-browser.
- The overlay is removed immediately after each screenshot and is isolated to proof capture. It does not ship in either application.
