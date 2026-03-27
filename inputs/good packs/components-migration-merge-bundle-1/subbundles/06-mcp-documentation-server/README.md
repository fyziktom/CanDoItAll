# 06 MCP Documentation Server

## Objective

Create `CanDoItAll.Mcp.Components` so agents can query shared component documentation, examples, parameters, and usage guidance directly from the new libraries.

## Exact Source References

Existing MCP patterns in CanDoItAll:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Tools\CanDoItAllTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.SshOps\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.SshOps\Tools\SshOpsTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Core`

Shared component sources:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Common`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox`

## Recommended Scope

Start read-only. Do not make this an editing server.

## Suggested Tools

- `components_search`
  - search by component name, keyword, prop, scenario
- `component_get`
  - return summary, namespace, parameters, events, dependencies
- `component_examples`
  - return curated sandbox example references
- `component_groups_list`
  - return sandbox grouping and page locations
- `component_css_tokens_get`
  - return owned CSS/token notes for a component
- `canvas_contract_get`
  - return canvas models, event args, or surface contract details

## Documentation Sources

Priority order:

1. explicit hand-written metadata files owned with the component
2. sandbox examples and scenario descriptors
3. public parameter reflection if useful
4. curated notes about migration or compatibility behavior

Do not depend on XML documentation comments. This repo should not need them just to power the MCP server.

## Implementation Steps

1. Mirror the current CanDoItAll MCP bootstrapping pattern:
   - `AddMcpServer()`
   - `Tools/*`
   - options validation
2. Create a small index model for components, groups, and examples.
3. Populate the index from `BaseLib`, `CanvasLib`, and `Sandbox`.
4. Expose read-only tools only.
5. Add tests that prove common queries resolve to the right components/examples.

## Acceptance Checklist

- the server starts with the same MCP pattern already used in CanDoItAll
- tools can answer shared component usage questions without opening app-specific sources
- sandbox examples are discoverable through the MCP server
- the server does not expose write operations

## Proof Required

- a sample MCP transcript or test output for each major tool
- test coverage for the documentation index
- a short explanation of how sandbox examples are linked to MCP results

## Suggested Agent Prompt

```text
Implement subbundle 06 only.

Create CanDoItAll.Mcp.Components as a read-only MCP server for the new shared component libraries. Follow the existing CanDoItAll MCP server pattern instead of inventing a new host shape. Index BaseLib, CanvasLib, and Sandbox examples so agents can ask how to use shared components without scanning the whole solution.
```
