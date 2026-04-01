# QA Prompt

Validate the active implementation against `C:\repositories\CanDoItAll\project-structure-canvas-context-menu-shortcuts-bundle-v1\inputs\00-original-request.md` and `C:\repositories\CanDoItAll\project-structure-canvas-context-menu-shortcuts-bundle-v1\inputs\02-structured-input.md`.

QA expectations:

- Use real browser validation on the project-structure canvas route, not only component tests.
- Prove that documented shortcuts match the actual keys the runtime listens for.
- Verify nested submenu progression by keyboard, including at least one path that opens a submenu and one path that executes a leaf.
- Inspect the open help overlay and confirm the help-page structure and shortcut guidance match the shipped menu behavior.
- Record screenshots, viewports, and Playwright actions in the execution report as soon as the pass finishes.
- Treat any mismatch between rendered underline, accessible label, help docs, and actual runtime behavior as a failure.
