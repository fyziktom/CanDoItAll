# CanvasLib Ownership

`CanDoItAll.Components.CanvasLib` is the canonical shared canvas implementation for active CanDoItAll consumers.

Current direct project references:
- `CanDoItAll.Components`
- `CanDoItAll.Components.Sandbox`
- `CanDoItAll.Mcp.Components`
- `CanDoItAll.Modules.Factory`
- `CanDoItAll.Modules.Workbench`
- `CanDoItAll.Web`

Rules:
- Put new shared canvas runtime work here.
- Keep public runtime behavior stable for ProjectStructure and PromptFactory.
- Treat other canvas trees as compatibility-only until a measured consolidation plan is approved.
