# Implementation Prompt

```text
Implement only the current subbundle from C:\repositories\CanDoItAll\project-structure-node-recomposition-bundle-1.

Constraints:
- Keep changes minimal and strongly typed.
- Do not add automatic background layout.
- Recomposition changes positions only. Do not reconnect, reparent, or otherwise alter graph relationships.
- Treat the selected node as the recomposition root and keep it anchored unless the subbundle explicitly says otherwise.
- Use the existing workbench page and service seams instead of introducing unnecessary layers.

Quality bar:
- Add or update targeted automated tests for the exact seam you changed.
- Preserve existing toolbar and canvas patterns.
- If implementation reality requires a scope correction, repair the bundle before proceeding.
```
