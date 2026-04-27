# Target Solution

## Service And Host Pattern

- Register `DialogService`, `TooltipService`, and `NotificationService` as scoped services in `AddCanDoItAllBaseLib`.
- Mount hosts once in app layout:
  - `<DialogHost />` for service-driven dialogs.
  - `<Tooltip />` for service-driven tooltips.
  - `<Notification />` for notifications.
- Keep existing direct `<Dialog IsOpen="...">` usage intact.

## Dialog Design

- `DialogService` owns an ordered collection of `DialogReference` instances.
- `OpenAsync` returns a task completed by `DialogService.Close(result)` or `DialogReference.CloseAsync(result)`.
- `DialogHost` renders each reference through the existing `Dialog` component and cascades the dialog reference to dynamic component content.
- `DialogOptions` maps to existing BaseLib modal properties: `Size`, `Title`, `Subtitle`, `Eyebrow`, `HelpText`, `CloseOnBackdrop`, `ShowCloseButton`, `DenseChrome`, `HeaderActions`, `Footer`, `TestId`, and extra classes.
- Dialog close paths must complete only the topmost dialog unless closing a specific reference.

## Tooltip Design

- `TooltipService` owns one active tooltip at a time.
- `TooltipOptions` supports position, duration, delay, close-on-leave, text, render-fragment content, and optional test id.
- `Tooltip` host renders a fixed-position Tailwind popover from pointer/focus coordinates and hides it on close/navigation/duration.
- A `TooltipTarget` helper may wrap triggers for ergonomic sandbox and product usage without requiring page-local overlay markup.

## Notification Design

- `NotificationService` owns an observable or event-backed message list.
- Existing `Notify(NotificationMessage)` remains supported.
- Convenience overloads create messages with severity, summary, detail, duration, payload, click/close callbacks, close-on-click, and optional persistence.
- `Notification` host renders the current message list with aria-live behavior, dismiss buttons, and Tailwind status tones.

## Styling Boundary

- Overlay chrome must use literal Tailwind classes in Razor and existing BaseLib layout primitives.
- No new Radzen CSS, SCSS, or app-specific custom CSS selectors should be introduced for the service chrome.
- If Tailwind output changes, regenerate `src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css`.

## Validation Strategy

- Add bUnit tests for service state, host rendering, close/result semantics, tooltip lifecycle, and notification lifecycle.
- Use the sandbox route as the real browser proof surface.
- Record Playwright MCP screenshots and assertions in `reviews/01-execution-report.md`.
