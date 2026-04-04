# CanDoItAll.Mcp.Components layout knowledge and component guidance

## Status

- `Completed`

## Objective

- Extend `CanDoItAll.Mcp.Components` so it can answer with practical layout guidance, preferred `Grid` / `Row` / `Column` / `Stack` usage patterns, and real shared-component usage examples drawn from sandbox and product code.

## Covered Inputs

- User request to add the learned layout knowledge into the components MCP server.
- User request to analyze whether the MCP already has actual data about other components.
- User note that `CanDoItAll.Web` already uses many components well and should inform the MCP guidance.
- Shared goal to avoid custom structural styles where shared components can express the layout.

## Prerequisites

- Subbundle `02-sandbox-layout-example-page-and-registry-updates` should provide the curated composition route referenced by the guidance.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Catalog\ComponentCatalogModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Catalog\ComponentCatalogService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Tools\ComponentsTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox`

## Deliverables

- New or enriched MCP responses that include layout composition guidance.
- Consumer example discovery sourced from real `.razor` files, not only sandbox metadata.
- Explicit guidance for when to use `Stack` versus `Grid`, when `Row` / `Column` should inherit parent tracks, and how to avoid custom structural CSS.

## Dependency Impact

- The new repo skill and plugin guidance depend on the MCP exposing actionable answers.
- Weak MCP answers here would keep Codex users falling back to ad-hoc markup instead of the shared layout system.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Extend the catalog data model with layout guidance and consumer-example metadata.
2. Index real component usage from sandbox and product `.razor` files.
3. Add tool coverage or enrich existing tool responses so Codex can retrieve the new guidance.
4. Verify the MCP still builds and returns the new data for shared layout components.

## Scope Exceptions

- Do not broaden this phase into design-token authoring or unrelated component rewrites.
- Do not attempt to document every possible component behavior manually when it can be discovered from code.

## Do Not Do

- Do not keep the MCP limited to sandbox-only examples when real product evidence exists.
- Do not encode the guidance as vague prose without structured example references.
- Do not hardcode Zyphonote-specific rules into the shared MCP.

## Acceptance Checklist

- The MCP can surface layout guidance for `Grid`, `Row`, `Column`, and `Stack`.
- The MCP can surface real usage examples from consumer code, including `CanDoItAll.Web`.
- The guidance explicitly steers callers away from unnecessary custom structural CSS.
- Existing component search and detail tools continue to work.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\CanDoItAll.Mcp.Components.csproj -v:minimal`
- Local MCP run or invocation proving the new guidance is present.
- Example MCP output for at least one layout component and one non-layout component with discovered usage references.

## Browser Validation Logging

- `N/A` unless the MCP work requires a browser-visible sandbox proof tied to the new example route.

## Progression Gate

- The components MCP must return structured layout guidance and real usage references before the installer and skill/plugin surfaces can close.

## Suggested Agent Prompt

```text
Implement this subbundle only. Extend CanDoItAll.Mcp.Components so it can return structured layout guidance plus real shared-component usage examples from sandbox and product code, then prove the new MCP responses locally.
```
