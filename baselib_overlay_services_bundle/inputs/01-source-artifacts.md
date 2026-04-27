# Source Artifacts

## Local Repository Sources

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals\Dialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\Tooltip.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\Notification.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\AlertPrimitives.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Infrastructure\ServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Overlays.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Feedback.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\SandboxCatalogRegistry.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Program.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\NotificationTests.cs`

## Radzen Reference Sources

- `C:\repositories\radzen-blazor\Radzen.Blazor\DialogService.cs`
- `C:\repositories\radzen-blazor\Radzen.Blazor\RadzenDialog.razor`
- `C:\repositories\radzen-blazor\Radzen.Blazor\TooltipService.cs`
- `C:\repositories\radzen-blazor\Radzen.Blazor\RadzenTooltip.razor`
- `C:\repositories\radzen-blazor\Radzen.Blazor\NotificationService.cs`
- `C:\repositories\radzen-blazor\Radzen.Blazor\RadzenNotification.razor`

## MCP Inventory Evidence

- Components MCP identifies `Dialog`, `Tooltip`, and `Notification` as BaseLib components.
- `Dialog` currently has direct component usage across product modules but no service host.
- `Tooltip` currently has only a hidden host placeholder and one sandbox usage.
- `Notification` already has a simple scoped service, but it lacks collection state, convenience overloads, payload/click/close support, clear/dismiss APIs, and richer host behavior.
