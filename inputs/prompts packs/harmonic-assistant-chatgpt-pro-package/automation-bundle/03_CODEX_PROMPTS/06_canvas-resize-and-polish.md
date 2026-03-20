# 06 — Make canvas responsive (ResizeObserver) + minor UX polish

Goal: ensure the canvas stays crisp and correctly scaled on resize.

## Files to modify
- `src/App.Blazor/Pages/Harmony.razor`
- `src/App.Web/wwwroot/harmonicAssistantCanvas.js` (only if needed)
- `src/App.Blazor/wwwroot/app.css` (optional)

## 1) Add ResizeObserver wiring (Blazor side)
In `Harmony.razor`:
- After `CanvasInterop.InitializeAsync(canvasRef)`, register a resize observer via JS interop.
Options:
A) Add a small JS helper function in `harmonicAssistantCanvas.js`:
   - `export function observeResize(id, element) { ... }`
   - Use `ResizeObserver` to call `resize(id)` when element size changes.
   - Store observer on renderer and dispose it.
B) Or use window resize event.

Preferred: ResizeObserver to handle container layout changes.

## 2) Ensure DPR scaling remains correct
Verify `resizeInternal`:
- uses `devicePixelRatio`
- sets `canvas.width/height` * dpr
- calls `context.setTransform(dpr,0,0,dpr,0,0)`
This keeps text crisp.

## 3) Cursor + touch-action polish (optional but recommended)
CSS:
- `.harmony-canvas { touch-action: none; cursor: default; }`
- On hoverable controls, set cursor to pointer from JS (or via CSS if using separate overlay).

## Acceptance criteria
- Resizing the browser or rotating a phone keeps the graph fitted and readable.
- No blur due to incorrect DPR handling.
- No memory leak: observer removed on dispose.

## Self-check
- Manual: resize the window while on `/harmony`.
- Verify `dispose` cleans observers.
