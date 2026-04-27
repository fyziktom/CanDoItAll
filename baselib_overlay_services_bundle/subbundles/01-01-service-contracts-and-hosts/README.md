# 01-service-contracts-and-hosts

## Status

- `Completed`

## Objective

- Establish the shared BaseLib service/host foundation for dialog, tooltip, and notification overlays without breaking existing direct component usage.

## Covered Inputs

- R1: missing proper services.
- R2: Radzen-inspired service/host pattern with Tailwind-only output.
- R3: preserve the existing component structure.

## Prerequisites

- Bundle readiness gate has passed.
- Current BaseLib and sandbox source references are available.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals\Dialog.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\Tooltip.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\Notification.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\AlertPrimitives.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Infrastructure\ServiceCollectionExtensions.cs
- C:\repositories\radzen-blazor\Radzen.Blazor\DialogService.cs
- C:\repositories\radzen-blazor\Radzen.Blazor\TooltipService.cs
- C:\repositories\radzen-blazor\Radzen.Blazor\NotificationService.cs

## Deliverables

- Add BaseLib service primitives and host components for service-driven overlays.
- Register all overlay services in `AddCanDoItAllBaseLib`.
- Keep existing direct `Dialog` and `Notification` usages source-compatible.
- Add focused tests for registration and host subscription basics.

## Dependency Impact

- Dialog, tooltip, notification behavior, sandbox examples, and Playwright proof all depend on this phase.
- Weak host subscription or DI registration would invalidate all downstream validation.

## Validation Depth

- Critical UI foundation.

## Implementation Steps

1. Add service model files for dialog and tooltip.
2. Upgrade notification service primitives while preserving existing names.
3. Add `DialogHost` and make `Tooltip` a real host.
4. Update `ServiceCollectionExtensions`.
5. Add or update focused component tests for service registration and host rendering.

## Scope Exceptions

- Side dialogs, draggable/resizable dialogs, and full Radzen feature parity are out of scope.

## Do Not Do

- Do not remove the direct `Dialog` component API.
- Do not add Radzen runtime dependencies.
- Do not introduce new custom CSS selectors for the service chrome.

## Acceptance Checklist

- `DialogService`, `TooltipService`, and `NotificationService` are scoped services.
- Host components subscribe and unsubscribe safely.
- Existing `Dialog.razor` parameters still compile.
- Existing `NotificationService.Notify(NotificationMessage)` callers still compile.

## Proof Required

- `dotnet build src/CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- Focused component tests for service registration and host mounting.
- Execution report gate row updated.

## Browser Validation Logging

- Route: `N/A` for this foundation unless host mounting changes are visible in sandbox during this subbundle.
- Viewport: `N/A`.
- Actions/assertions: `N/A`; browser validation is required in subbundle 04.
- Screenshots: `N/A`.
- Review questions: `N/A`.

## Progression Gate

- Downstream work may proceed only after BaseLib builds and service registration/host subscription tests pass.

## Completion Proof

- Added scoped `DialogService`, `TooltipService`, upgraded `NotificationService`, `DialogHost`, and host wiring through `AddCanDoItAllBaseLib`.
- Preserved existing direct `Dialog` and `Notification` APIs while adding service-driven state.
- `dotnet build src\CanDoItAll.Components.BaseLib\CanDoItAll.Components.BaseLib.csproj` passed with 0 warnings and 0 errors.
- Focused component tests passed as part of the final 12-test overlay service run.

## Suggested Agent Prompt

```text
Implement only the BaseLib service contracts, host foundation, DI registration, and focused host/service tests. Preserve existing direct component APIs and do not add Radzen dependencies.
```
