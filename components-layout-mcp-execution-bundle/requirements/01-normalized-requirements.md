# Normalized Requirements

## R001 Zyphonote Progress Cleanup

- Remove the comparison-only Stack, Grid, and fixed Row/Column examples from `C:\repositories\zyphonote\src\App.Blazor\Pages\Progress.razor`.
- Keep the responsive Row/Column version on the page as the shipped pattern.

## R002 Dedicated Sandbox Layout Example

- Create a dedicated components sandbox page that demonstrates:
  - layout composition with `Stack`
  - layout composition with `Grid`
  - layout composition with `Grid` + `Row` + `Column`
  - responsive `Grid` + `Row` + `Column` behavior
- Make the page discoverable through the sandbox catalog and examples.

## R003 Components MCP Layout Guidance

- Add explicit guidance for `Grid`, `Row`, `Column`, and `Stack` into `CanDoItAll.Mcp.Components`.
- Capture the composition rules learned from the recent fix:
  - prefer shared layout primitives over ad-hoc markup
  - `Grid` owns track definitions
  - `Row` inherits or overrides grid tracks for a full-width nested layout
  - `Column` should align content and span tracks instead of acting as a raw wrapper
  - custom styles should be visual, not structural, wherever possible

## R004 Components MCP Real Examples

- Analyze and improve whether the component MCP already has real component usage data.
- Include actual shared-component usage examples from `CanDoItAll.Web` or related CanDoItAll modules so the server offers more than sandbox-only examples.

## R005 Install Path

- Ensure the normal CanDoItAll MCP reinstall flow publishes and wires the components MCP server.
- Update repo config output, user config output, and the install manifest accordingly.
- Prove the install locally on this machine.

## R006 Codex Guidance

- Add repo-managed guidance that tells future agents to use the component MCP when working with CanDoItAll shared components.
- Include a skill and a Codex plugin surface for that guidance.
- The guidance must reinforce the preference for shared components and minimal custom structural CSS.
