# Canvas Widget System (v2.1)

Goal: Keep advanced UX inside Canvas without turning JS into business logic.

## Concept
- JS canvas owns:
  - widget layout + rendering
  - pointer hit testing
  - UI state for sliders/toggles/buttons (lightweight)
- C# owns:
  - module settings persistence
  - heavy computations
  - module enable/disable states
  - session recording/import/export

## Widget Registry
In JS:
- `renderer.widgets = [{ id, title, rect, items: [...] }, ...]`
- Each widget item has:
  - `kind`: "button" | "toggle" | "slider" | "dropdown" | "label"
  - `id`: stable identifier
  - `value`: current
  - `meta`: min/max/step/options
  - `rect`: computed in layout pass

Expose a single callback:
- `dotNetRef.invokeMethodAsync("OnCanvasWidgetEvent", widgetId, itemId, eventKind, value)`

## Layout Rules
- Widgets anchored in corners:
  - Left-top: Mood + Module list (collapsible)
  - Right-top: Text size + Graph zoom + History window
  - Right-bottom: Recording + import/export
  - Left-bottom: MIDI status + latency + chord confidence indicator
- Widgets must not overlap the graph; reserve top padding via margins.

## Minimal Blazor UI
Keep only:
- canvas element
- (optional) a small debug text line for last chord (can be toggleable)
Everything else must be moved into canvas widgets.

## Accessibility
- Minimum hit target: 34x34 px
- Keyboard accessibility is optional for now (canvas), but keep high contrast and tooltips.

