# QA Prompt

Validate the floating component toolbox migration using real browser behavior.

- Open each relevant route with Playwright MCP.
- Confirm the toolbox window is visible, readable, unclipped, and layered above the canvas/WebGL stage.
- Add a project structure block from the toolbox and confirm the block appears in the canvas.
- Add a role from the WebGL toolbox and confirm the new role/person appears in 3D.
- Smoke test process canvas toolbox and prompt factory toolbox after migration.
- Capture screenshots under `C:\repositories\CanDoItAll\output\playwright-mcp\floating-component-toolbox`.
- Record route, viewport, actions, assertions, screenshots, and pass/fail result in `reviews/01-execution-report.md`.
