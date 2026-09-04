# Consumer instruction — reference only

This bundle is not an implementation task. Do not modify product code merely because this
bundle was opened.

When preparing or executing a child bundle in the UI component seam program:

1. Read the root `AGENTS.md`, current repository instructions, and this bundle.
2. Confirm the child bundle contains the reference ID `CDA-UI-SEAMS-BASE-v1`.
3. Refresh `development` and record the current baseline. The commit recorded in this
   bundle is evidence from preparation, not an execution pin.
4. Read the applicable bookmarkability source material under
   `inputs/bookmarkability/`, especially the state taxonomy and route-owner rules.
5. Inventory the selected component cluster before proposing layers.
6. Preserve physical component placement unless the child bundle explicitly owns a
   project-extraction outcome.
7. Prefer the smallest real seam:
   - pure policy/mapper/reducer with no interface;
   - one feature-scoped controller/facade for a coherent multi-service UI workflow;
   - an interface only at a genuine I/O, host, navigation, or substitution boundary.
8. Do not introduce:
   - wrapper pyramids;
   - interface-per-method abstractions;
   - service-bag facades;
   - a generic component lifecycle base;
   - new partial files as the final architecture;
   - direct EF or `IServiceProvider` access in Razor components;
   - feature references from `CanDoItAll.AppComponents`;
   - route construction inside child components;
   - permanent tests that freeze source layout.
9. Preserve existing user-visible behavior unless the child bundle explicitly changes it.
10. Make route-significant state controlled by the page/workspace even if URL binding is
    deferred.
11. Make the component renderable with explicit state and minimal fakeable dependencies.
12. Own validation and test cleanup in the child bundle. This shared base deliberately
    contains no product test command contract.
13. At closure, report:
    - responsibilities removed from the Razor component;
    - remaining direct dependencies and why they remain;
    - state ownership after the change;
    - tests removed, rewritten, or added;
    - route-ready, sandbox-ready, and project-extraction-ready decisions;
    - any proposed update to this shared base.

Stop and repair the child bundle if it still requires guessing who owns state, which layer
owns an operation, whether a new abstraction is real, or how the result can later be
sandboxed.
