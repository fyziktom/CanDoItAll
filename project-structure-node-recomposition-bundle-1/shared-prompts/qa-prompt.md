# QA Prompt

```text
Validate the current subbundle from C:\repositories\CanDoItAll\project-structure-node-recomposition-bundle-1.

Required checks:
- Confirm the command is manual and selection-scoped.
- Confirm the recomposed subtree uses the available space around the selected node more efficiently than before.
- Confirm no nodes overlap after recomposition, including against untouched nodes.
- Confirm links, parent-child relationships, and node identities remain unchanged.
- Confirm positions persist after reload.

For UI-visible phases:
- Run a large-screen browser pass first.
- Capture screenshots and inspect them for spacing, collisions, clipping, and unused-space problems.
- Follow with a narrower-width pass on the same route.
- Record the route, viewport, Playwright actions, screenshots, and result in reviews/01-execution-report.md.
```
