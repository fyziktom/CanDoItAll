# Target Solution

## Design Direction

- Use tabs for intent-level grouping, not decorative segmentation.
- Use `Grid` for related fields and `Stack` for vertical section flow.
- Use existing `FormField`, `FormSection`, `SurfaceCard`, `PanelCard`, `Cluster`, `Split`, `StatusBadge`, `Button`, and `EmptyState` components.
- Keep child setup editors as child components; improve their internal density only where it reduces the parent form problem.

## Processes Layout

- `ProcessStepEditorForm.razor` owns a presentation-only selected tab index.
- Basic info tab: key, title, subtitle, step kind, target lead hours, subprocess definition, canvas-managed dependencies, and artifact input summaries.
- Execution tab: manual skip, safe refusal, approval, decision record, operation target scope, and allowed operations.
- Contracts tab: notes, input/output/evidence/decision/exception summaries.
- Routing tab: branch outcome list and add action.
- Roles tab: role assignment list and add action.
- Artifacts tab: artifact expectation list and add action.

## Workflows Layout

- `WorkflowCanvasEditor.razor` owns a presentation-only inspector tab index.
- Definition tab: workflow name, description, runtime backend.
- Node setup tab: selected node identity, kind-specific basics, executor selection, descriptor summary, execution policy, executor-specific settings, input/result shape, and instructions. Keeping executor settings in this tab avoids duplicating selected-node event-handler scope.
- Routes tab: edge builder and existing route rows.
- Preview tab: validation issues, preview input JSON, and preview result.

## Boundaries

- No changes to `WorkflowDefinition`, `WorkflowCanvasDocument`, process editor models, persistence entities, services, or runtime orchestration.
- No custom page-wide theme or new visual palette.
- Minimal local CSS is allowed only for layout gaps the existing components cannot express in the workflow inspector.
