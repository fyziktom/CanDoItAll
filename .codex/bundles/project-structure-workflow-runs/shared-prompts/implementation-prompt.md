# Implementation Prompt

Implement the selected subbundle only.

Before editing, verify the subbundle prerequisites, exact source references, and dependency gates. Keep changes narrowly scoped to the listed ownership. Preserve strict separation between workflow runtime contracts, project-structure services, web API endpoints, and Blazor UI. Prefer typed records, enums, and id wrappers over metadata string keys.

Use the process-start path as an implementation reference, but do not copy process staffing or matching-resource behavior into workflow start. Workflow start from project structure is confirmation-only.

For UI work, query the CanDoItAll components MCP before adding structural markup. Prefer existing project-structure overlay/dialog and BaseLib layout primitives. Keep Razor components focused on rendering and orchestration.

Proof is part of the work. Update `reviews/01-execution-report.md` with commands, tests, browser artifacts, scenario results, and gate decisions. Stop and repair the bundle if implementation reality invalidates the dependency map, the 20-scenario proof plan, parentage rules, or the project/parent input invariant.
