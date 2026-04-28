# 04-sandbox-docs-and-browser-proof

## Status

- `Completed`

## Objective

- Add sandbox examples, docs, Tailwind output, and Playwright MCP validation evidence proving the new overlay services work in a real browser.

## Covered Inputs

- R4: add sandbox examples and update docs.
- R5: validate with Playwright MCP, especially dialog show/sizing/close/returned object cases.
- R6: complete bundle workflow closure.

## Prerequisites

- `01-01-service-contracts-and-hosts` completed.
- `02-02-dialog-service-behavior` completed.
- `03-03-tooltip-notification-services` completed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Layout\MainLayout.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Overlays.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Feedback.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\SandboxCatalogRegistry.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\README.md
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\README.md
- C:\repositories\CanDoItAll\Tailwind\input.css

## Deliverables

- Sandbox layout mounts service hosts centrally.
- Overlay/feedback sandbox pages demonstrate dialog, tooltip, and notification service APIs.
- Sandbox registry includes examples discoverable through the existing catalog.
- BaseLib and sandbox docs describe setup, host placement, service usage, and validation commands.
- Tailwind output regenerated if class inventory changes.
- Execution report updated with Playwright MCP browser analytics and raw-note closure.

## Dependency Impact

- This is the final closure phase; weak browser proof leaves the entire user request unresolved.

## Validation Depth

- End-to-end UI proof and bundle closure.

## Implementation Steps

1. Update sandbox layout host mounting.
2. Add dialog service demo controls for compact, medium/wide/full, close button, backdrop, and returned object scenarios.
3. Add tooltip and notification service demo controls.
4. Update docs and Tailwind output.
5. Run builds, focused tests, and Playwright MCP route validation.
6. Update execution report and final bundle statuses.

## Scope Exceptions

- Product app route adoption is not required in this phase; docs should explain host placement for adopters.

## Do Not Do

- Do not close this phase without Playwright MCP screenshots.
- Do not use screenshots alone without action/assertion notes.
- Do not leave raw-note closure rows pending.

## Acceptance Checklist

- Sandbox examples are discoverable and runnable.
- Docs explain registration, hosts, and service usage.
- Browser proof validates dialog sizes and returned objects.
- Browser proof validates tooltip and notification open states.
- Final bundle validation passes.

## Proof Required

- `npm run tailwind:build`
- `dotnet build src/CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `dotnet build src/CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj`
- Focused component tests for dialog, tooltip, and notification services.
- Playwright MCP screenshots and assertions recorded in execution report.
- `python C:\Users\dell\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py baselib_overlay_services_bundle --profile initiative --stage completed`

## Browser Validation Logging

- Route: `http://localhost:{port}/groups/overlays`.
- Viewports: `1600x1000` first, then mobile width after desktop passes.
- Actions/assertions: open each dialog size, close with result object, verify result summary, open tooltip, trigger notification, dismiss notification, capture screenshots.
- Screenshots: `output/playwright-mcp/baselib-overlay-services-desktop.png`, `output/playwright-mcp/baselib-overlay-services-mobile.png`.
- Review questions: no clipping, readable text, proper z-order, modal sizes clear, service results visible, mobile layout coherent.

## Progression Gate

- Bundle may close only when browser analytics, subbundle gate rows, raw-note closure rows, builds, tests, and final validator all agree.

## Completion Proof

- Mounted `DialogHost`, `Tooltip`, and `Notification` centrally in the sandbox layout.
- Added overlay service examples, feedback notification examples, registry entries, and docs for BaseLib and sandbox usage.
- Ran `npm run tailwind:build`, BaseLib build, Sandbox build, and focused overlay component tests.
- Playwright MCP captured `baselib_overlay_services_bundle/proof/baselib-overlay-services-desktop-dialog.png` and `baselib_overlay_services_bundle/proof/baselib-overlay-services-mobile-dialog.png`; final browser smoke verified desktop returned-object closure and mobile dialog fit/clickability with no new browser console errors.

## Suggested Agent Prompt

```text
Implement only sandbox examples, docs, Tailwind output, browser validation, and bundle closure updates. Use Playwright MCP for real browser proof and record every result in the execution report.
```
