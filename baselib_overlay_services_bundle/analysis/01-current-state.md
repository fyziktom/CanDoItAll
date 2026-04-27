# Current State

## BaseLib Components

- `Dialog.razor` is a reusable controlled modal component with `IsOpen`, `Size`, title/subtitle/help, header actions, footer content, close button, and backdrop close support.
- `Dialog.razor` does not have a service-driven host or task-returning API.
- `Tooltip.razor` is a hidden placeholder host only; it has no service, state model, or visible tooltip behavior.
- `Notification.razor` subscribes to a simple `NotificationService.Notification` event and renders transient toasts.
- `NotificationService` only exposes `Notify(NotificationMessage)` through an event and does not store messages or expose dismiss/clear/click/close operations.
- `AddCanDoItAllBaseLib` currently registers only `NotificationService`.

## Sandbox

- The sandbox layout mounts `<Notification />`.
- The overlays page renders `<Dialog />` and `<Tooltip />` as placeholder host proof, but it does not demonstrate programmatic service usage.
- The feedback page references notification examples but does not prove richer service lifecycle behavior.
- The sandbox registry already has `Feedback` and `Overlays` groups appropriate for service examples.

## Radzen Reference Pattern

- Radzen registers scoped services and mounts host components in the app layout.
- `DialogService` raises open/close events, tracks task completion sources, and returns close results to callers.
- `TooltipService` raises open/close events and host components render the current tooltip.
- `NotificationService` owns a message collection and `RadzenNotification` listens for collection changes.
- CanDoItAll should borrow the service/host architecture, not Radzen CSS, JS, naming, or full feature breadth.

## Existing Validation Surface

- `tests/CanDoItAll.Tests.Components/NotificationTests.cs` verifies notification z-index above modal overlays.
- There are no focused component tests for `DialogService`, tooltip host behavior, or notification collection lifecycle.
- Browser validation must be added through the sandbox because service-driven overlays need real interaction proof.
