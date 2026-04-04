# Target Solution

## Shared Direction

- Zyphonote keeps only product-relevant UI.
- The components sandbox becomes the permanent comparison and teaching surface for layout composition.
- `CanDoItAll.Mcp.Components` becomes the discoverability layer for:
  - component metadata
  - sandbox examples
  - real app usage examples
  - practical composition guidance, especially for layout primitives

## Boundaries

- Do not reintroduce raw flex/grid wrappers when shared layout primitives can express the structure.
- Do not move visual styling rules into the component MCP; the MCP should explain usage and source examples, not duplicate CSS.
- Do not turn the sandbox page into a marketing layout. It should be an operational proof surface for shared components.

## Expected Deliverables

- Updated `Progress.razor` in Zyphonote.
- New or updated sandbox route plus registry coverage in CanDoItAll.
- Expanded component MCP catalog model and/or tools to surface layout guidance and real consumer examples.
- Updated MCP reinstall script and manifest output to include the components MCP.
- New repo-managed skill, plugin metadata, and documentation for using the component MCP correctly.
