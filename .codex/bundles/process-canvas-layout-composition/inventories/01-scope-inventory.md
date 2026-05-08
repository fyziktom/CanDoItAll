# Scope Inventory

| Area | Current State | Bundle Impact |
| --- | --- | --- |
| Definition recomposition | `ProcessCanvasRecompositionService.ApplySmartRecomposition` builds step, role, and branch boxes. | Primary implementation target. |
| Fallback coordinates | `ProcessCanvasSurfaceFactory.Coordinates` provides non-recomposed defaults. | Review only unless first-render defaults must match the new rules. |
| Canvas links | `ProcessCanvasSurfaceFactory.Links` projects flow, role, artifact, and messaging links. | Input to layout reasoning; avoid behavioral changes. |
| Toolbar action | `ProcessCanvasToolbarActions.razor` exposes `Recomposition`. | No UI redesign expected. |
| Persistence | `ProcessWorkspace.Canvas.Persistence.cs` saves recomposed coordinates. | Must remain unchanged. |
| Tests | `ProcessCanvasRecompositionServiceTests` already covers the recomposer. | Extend targeted geometry assertions. |
| Browser proof | `/processes` or `/projects/{projectId}/processes` route renders the process canvas. | Required for final UI closure when app launch is available. |
