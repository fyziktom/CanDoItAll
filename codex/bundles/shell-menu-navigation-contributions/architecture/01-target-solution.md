# Target Solution

- Add a shared-kernel navigation contribution model:
  - `IShellNavigationContributor` exposes module-owned menu contributions.
  - `ShellNavigationContribution` carries a parent route, the contributed `ShellNavigationItem`, ordering, and an `IsSubItem` marker.
  - A `DesignNote` field preserves the reason the metadata exists while the current visual treatment remains flat.
- Update the Web shell composition to merge contributors after their parent route, then apply existing badges and active-route matching against the merged list.
- Register an AgentFramework contributor from the AgentFramework module service collection. It contributes `Workflows` after `/agents` and marks the item as a subitem for future visual nesting.
- Update `MainLayout` to inject the contributors and pass them into navigation item creation and active route matching.
- Apply the shared delayed tooltip value to standard menu item tooltips and the bottom Settings tooltip. Do not restore tooltips on popup trigger items.
- Validate with targeted tests for contribution ordering and route matching, then Playwright MCP desktop proof for menu order and delayed tooltip timing.
