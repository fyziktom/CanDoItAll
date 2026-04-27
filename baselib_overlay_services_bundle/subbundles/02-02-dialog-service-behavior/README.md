# 02-dialog-service-behavior

## Status

- `Completed`

## Objective

- Implement robust dialog service behavior with component and fragment content, modal sizing, close paths, and task-returned result objects.

## Covered Inputs

- R1: missing proper `DialogService`.
- R2: Radzen-inspired service API and host behavior.
- R5: dialog sizing, showing, closing, and returned object validation.

## Prerequisites

- `01-01-service-contracts-and-hosts` completed with passing foundation proof.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals\Dialog.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Infrastructure\ServiceCollectionExtensions.cs
- C:\repositories\radzen-blazor\Radzen.Blazor\DialogService.cs
- C:\repositories\radzen-blazor\Radzen.Blazor\RadzenDialog.razor
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\NotificationTests.cs

## Deliverables

- `DialogService` with open async overloads, close APIs, topmost close behavior, cancellation-safe tasks, and navigation cleanup.
- `DialogReference` and `DialogOptions` models for result completion and UI configuration.
- `DialogHost` rendering through the existing Tailwind `Dialog` component.
- Focused dialog service tests.

## Dependency Impact

- Sandbox examples and final Playwright proof depend on correct returned-object and size behavior.
- Incorrect close semantics would make downstream proof untrustworthy.

## Validation Depth

- Critical UI behavior foundation with component tests and browser proof.

## Implementation Steps

1. Implement `DialogService`, `DialogReference`, and `DialogOptions`.
2. Render service dialogs in `DialogHost` using existing `Dialog` and `DynamicComponent`.
3. Cascade dialog references to dynamic component content.
4. Add tests for open async, close result, topmost close, backdrop-disabled behavior, size option mapping, and component/fragment content.
5. Keep any user-facing classes literal Tailwind classes.

## Scope Exceptions

- Dragging, resizing, side dialogs, and animations are excluded from this phase.

## Do Not Do

- Do not migrate product module dialogs in this phase.
- Do not complete a dialog task twice.
- Do not replace existing controlled `Dialog` usage with service-only semantics.

## Acceptance Checklist

- `OpenAsync` returns the object supplied to `Close`.
- Close button and backdrop close respect options.
- Multiple modal sizes map to visible Tailwind width behavior.
- Component and render-fragment content both render.
- Dynamic component content can close via cascaded dialog reference or service topmost close.

## Proof Required

- Focused dialog component tests pass.
- `dotnet build src/CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- Playwright MCP proof in subbundle 04 exercises size variants, close paths, and returned object display.

## Browser Validation Logging

- Route: `http://localhost:{port}/groups/overlays`.
- Viewports: `1600x1000` first, then a mobile-width follow-up.
- Actions/assertions: open compact, medium, wide/full dialogs; close with object result; close via button; test backdrop disabled/enabled behavior.
- Screenshots: `output/playwright-mcp/baselib-dialog-service-desktop.png`, `output/playwright-mcp/baselib-dialog-service-mobile.png`.
- Review questions: modal text readable, no clipping, size variants visible, close result displayed exactly once.

## Progression Gate

- Subbundle 04 may rely on dialog proof only after component tests pass and the sandbox exposes direct controls for size and returned-object scenarios.

## Completion Proof

- Added async dialog open/close APIs, `DialogReference`, `DialogOptions`, topmost close behavior, navigation cleanup, dynamic component rendering, and cascaded dialog references.
- Added tests for fragment results, component content, topmost close, non-component rejection, and modal option mapping.
- Playwright MCP validated compact, wide, and full sizing at `1600x1000`; verified returned object text `Dialog returned: Approved from service-demo`; verified backdrop-locked dialog survives backdrop click.

## Suggested Agent Prompt

```text
Implement only the dialog service behavior and focused tests. Preserve existing controlled Dialog component usage and prepare sandbox-testable APIs for modal sizing and returned-object closure.
```
