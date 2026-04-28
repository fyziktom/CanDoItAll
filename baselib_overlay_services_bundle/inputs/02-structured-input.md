# Structured Input

## Objectives

- Add service APIs that feel as ergonomic as Radzen while staying in CanDoItAll naming, BaseLib structure, and Tailwind-only rendering.
- Preserve existing direct `Dialog` component usage while adding a service-driven host for programmatic dialogs.
- Replace the placeholder tooltip host with a real service-driven tooltip surface.
- Upgrade notification behavior without breaking existing `NotificationService.Notify(NotificationMessage)` callers.
- Add sandbox examples that show service usage, edge cases, and host placement.
- Update BaseLib and sandbox docs.
- Validate dialog sizing, closing, and returned objects with Playwright MCP.

## Hard Constraints

- Styling must be expressed with Tailwind classes and existing BaseLib layout primitives, not new custom CSS classes for service chrome.
- Existing direct `Dialog` component consumers must keep compiling.
- Existing `Notification` host consumers must keep compiling.
- Service registrations belong in `AddCanDoItAllBaseLib`.
- Sandbox examples must use the established catalog/page structure.
- Browser validation must use Playwright MCP and include screenshots.

## Assumptions

- BaseLib may add new public types in the `CanDoItAll.Components.BaseLib` namespace.
- The sandbox can mount `DialogHost`, `Tooltip`, and `Notification` centrally in its layout.
- Product hosts can adopt the same host components later without forcing all direct dialog usage to migrate now.

## Validation Expectations

- `dotnet build src/CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `dotnet build src/CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj`
- Focused component tests for services and hosts.
- `npm run tailwind:build` after new Tailwind classes are introduced.
- Playwright MCP proof for the sandbox route covering dialog modal sizes, close button, backdrop behavior, returned object text, tooltip open state, notification show/dismiss, and responsive checks.
