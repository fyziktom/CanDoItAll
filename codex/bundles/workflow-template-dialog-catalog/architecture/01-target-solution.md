# Target Solution

## UI Shape

- Keep `WorkflowsPage` as the orchestration owner.
- Remove the `TabsItem Text="Templates"` section and any `TemplatesTabIndex` dependency.
- Add a `PageHeaderActionButton` or Workflows-tab-local `Button` labeled for templates, with an icon such as `content_copy` or `library_books`.
- Opening the catalogue sets dialog state and then calls the template loader.
- The catalogue dialog uses existing primitives:
  - `Dialog`
  - `Grid`
  - `SurfaceCard` only for semantically framed detail/empty/error areas
  - `Stack`
  - `Cluster`
  - `Button`
  - `StatusBadge`
  - `EmptyState`

## Lazy Loading

- Template state should move from tab-driven loading to dialog-driven loading.
- `WorkflowTemplatePackLoader.Load()` must not be called from initialization, refresh, tab change, computed properties, or component-library loading.
- The dialogue should show explicit loading/error states.

## Template Preview

- Selecting Preview should materialize a transient `WorkflowDefinition` from the selected template and a preview LLM component.
- The preview must be read-only from the user's perspective. If `WorkflowCanvasEditor` is reused, the preview dialog must avoid save actions and must not persist changes until "Add to my drafts".
- The preview dialog should be canvas-dominant, with template metadata and basic node/edge/IO details around it.

## Draft Adoption

- "Add to my drafts" persists:
  - an LLM component based on template instructions/model settings
  - a workflow definition in `WorkflowLifecycleStatus.Draft`
  - a user-facing description without the managed seed marker
- Naming algorithm:
  - base candidate: `template.Name`
  - if no existing definition has the base name, use the base name
  - otherwise use the first available two-digit prefix: `01 {base}`, `02 {base}`, ...
  - collision checks must ignore existing definitions with the same prefix format.

## Template Debranding

- Update `Templates/Workflows/workflows/default-workflows.yaml` so formerly SEAMARK workflows become generic offer/product-document analysis workflows.
- Remove company names, exact company-specific price facts, and sensitive source-specific labels.
- Preserve graph semantics: source ingestion -> LLM summary/extraction -> project-structure markdown asset -> end.

## Validation Boundary

- Component tests own behavior.
- Playwright large-screen proof owns real visual/dialog state.
- Generated design proposals are comparison inputs, not acceptance proof by themselves.
