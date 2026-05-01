# CanDoItAll.Mcp.Components

## Purpose

MCP adapter for discovering and inspecting shared component-library capabilities.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Mcp.Components/CanDoItAll.Mcp.Components.csproj
```

## References

Project references:

- `../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj`
- `../CanDoItAll.Components.Charts/CanDoItAll.Components.Charts.csproj`
- `../CanDoItAll.Components.Common/CanDoItAll.Components.Common.csproj`
- `../CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj`
- `../CanDoItAll.Mcp.Core/CanDoItAll.Mcp.Core.csproj`

Framework references:

- `Microsoft.AspNetCore.App`

Direct package references:

- `ModelContextProtocol (1.1.0)`

## Architecture Notes

This project is an adapter for agent-facing tooling. It must stay thin over canonical app/module services and should not become a second implementation of product behavior.

## Agent Usage Guidance

Agents should query `component_get` before composing shared UI so they use the component's source path, parameters, CSS notes, sandbox examples, and guidance instead of inventing local structure.

For overlay services, query:

- `component_get("Notification")` before adding or moving toast feedback.
- `component_get("Tooltip")` before opening tooltip content through `TooltipService`.
- `component_get("TooltipTarget")` before wrapping declarative hover or focus help.

Notification position should be chosen by what must remain unobscured. Keep `TopRight` for ordinary desktop feedback, move to `BottomCenter` when top chrome is dense or mobile reach matters, use side positions near rails or list/detail panes, and reserve `TopCenter` for global but still non-blocking messages. If the state requires a choice, use `DialogService` or an inline `Alert` instead of a notification.

Tooltip position should be chosen by the trigger's edge pressure and the next likely action. Prefer `Top` or `Right` when there is room, switch to `Bottom` near the top edge, use `Top` near lower toolbars, use `Left` or `Right` for dense inline controls, and use corner or edge placements near viewport, card, toolbar, or panel corners.

When an agent uses non-default notification or tooltip placement, it should validate the sandbox behavior with Playwright at the relevant desktop and mobile viewport sizes.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
