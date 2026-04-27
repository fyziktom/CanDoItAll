# 03-tooltip-notification-services

## Status

- `Completed`

## Objective

- Implement service-driven tooltip behavior and upgrade notification lifecycle behavior with accessible Tailwind host output.

## Covered Inputs

- R1: missing proper `TooltipService` and `NotificationService`.
- R2: Radzen-inspired service/host model with Tailwind-only rendering.
- R3: preserve existing BaseLib structure.

## Prerequisites

- `01-01-service-contracts-and-hosts` completed with passing foundation proof.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\Tooltip.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\Notification.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\AlertPrimitives.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Infrastructure\ServiceCollectionExtensions.cs
- C:\repositories\radzen-blazor\Radzen.Blazor\TooltipService.cs
- C:\repositories\radzen-blazor\Radzen.Blazor\RadzenTooltip.razor
- C:\repositories\radzen-blazor\Radzen.Blazor\NotificationService.cs
- C:\repositories\radzen-blazor\Radzen.Blazor\RadzenNotification.razor
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\NotificationTests.cs

## Deliverables

- `TooltipService`, `TooltipOptions`, tooltip state model, and optional `TooltipTarget` helper.
- `Tooltip` host rendering visible fixed-position Tailwind tooltip content.
- Upgraded `NotificationService` with stateful messages, convenience overloads, dismiss/clear, payload, click, close, and duration support.
- Updated `Notification` host with accessible live region and Tailwind status tones.
- Focused tooltip and notification tests.

## Dependency Impact

- Sandbox examples and browser proof depend on visible tooltip/notification open states.
- Notification changes must not break the existing z-index regression test.

## Validation Depth

- UI service behavior with component tests and browser proof.

## Implementation Steps

1. Implement tooltip service state and host rendering.
2. Add ergonomic `TooltipTarget` trigger wrapper if it reduces sandbox/page boilerplate.
3. Upgrade notification service primitives and host lifecycle.
4. Preserve existing notification API shape.
5. Add focused tests for open/close, dismiss/clear, payload/callbacks, and z-index.

## Scope Exceptions

- Element-reference JS measurement is excluded unless simple coordinate positioning cannot pass Playwright proof.

## Do Not Do

- Do not use Radzen CSS or JS.
- Do not remove the existing `<Notification />` host.
- Do not make tooltips permanent page content.

## Acceptance Checklist

- Tooltip text and render-fragment content can open and close.
- Tooltip host uses accessible `role="tooltip"` output and safe z-index above dialogs.
- Notifications can be added, dismissed, cleared, clicked, and closed with callbacks.
- Notification duration and persistent messages behave predictably.

## Proof Required

- Focused tooltip and notification component tests pass.
- Existing `NotificationTests` pass.
- Playwright MCP proof in subbundle 04 captures open tooltip and visible notifications.

## Browser Validation Logging

- Route: `http://localhost:{port}/groups/overlays` or `http://localhost:{port}/groups/feedback`.
- Viewports: `1600x1000` plus mobile follow-up if the route layout changes.
- Actions/assertions: open tooltip, assert visible tooltip text and no clipping; trigger notifications, assert messages visible; dismiss notification.
- Screenshots: `output/playwright-mcp/baselib-tooltip-notification-desktop.png`.
- Review questions: tooltip readable, notification stack not overlapping dialog, dismiss affordance visible.

## Progression Gate

- Subbundle 04 may proceed when tooltip/notification tests pass and sandbox triggers are available for Playwright MCP interaction.

## Completion Proof

- Added `TooltipService`, `TooltipTarget`, host-rendered fixed-position tooltip state, and upgraded notification messages with ids, summary/detail, payloads, callbacks, dismiss, clear, click, close, and duration support.
- Updated `Notification` host to render service state with accessible live-region output and Tailwind severity styles.
- Focused component tests passed for tooltip open/close, target trigger lifecycle, notification payload/click/close behavior, and host dismiss behavior.
- Playwright MCP validated visible tooltip text, pointer-leave close, visible notification toast, and dismiss removal.

## Suggested Agent Prompt

```text
Implement only tooltip service behavior and notification service upgrades with tests. Keep Tailwind-only host rendering and preserve existing notification API compatibility.
```
