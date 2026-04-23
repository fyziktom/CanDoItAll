# Normalized Requirements

## Requirements

- RQ-001: Provide a generic floating component toolbox shell usable by different canvas-like hosts with different component catalogs.
- RQ-002: Preserve project structure toolbox behavior, including search, grouped items, action IDs, window visibility, minimize/hide, and existing create flow.
- RQ-003: Preserve process canvas toolbox behavior, including role/step templates and existing editor flow.
- RQ-004: Preserve prompt factory components toolbox behavior, including search, sections/groups, add flow, and preview behavior.
- RQ-005: Add a WebGL floating component toolbox using the same generic toolbox principle.
- RQ-006: Add a WebGL toolbox action that creates a new role from the process role templates and makes it visible in the 3D scene.
- RQ-007: Keep the generic toolbox extensible for future project-structure-in-3D and process-in-3D catalogs.
- RQ-008: Validate with Playwright MCP screenshots and real interactions, including adding a project structure block and adding a WebGL role.

## Non-Requirements

- Do not replace CanvasLib right-click context menus with the floating component toolbox in this bundle.
- Do not persist WebGL sandbox role edits beyond the current in-memory sandbox model unless existing sandbox behavior already does so.
- Do not redesign the visual language beyond necessary shared styling normalization.
