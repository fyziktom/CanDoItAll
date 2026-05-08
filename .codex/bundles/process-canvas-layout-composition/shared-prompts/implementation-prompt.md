# Implementation Prompt

Implement the process canvas layout-composition bundle only.

Scope:
- Tune automatic definition-canvas recomposition in `ProcessCanvasRecompositionService`.
- Keep CanvasLib generic primitives intact unless a reusable collision behavior is truly required.
- Preserve process semantics, branch normalization, manual dragging, and persistence.
- Add focused tests in `ProcessCanvasRecompositionServiceTests`.

Required proof:
- Run the targeted component test class.
- Run a build or broader test slice if the targeted test command does not compile all touched projects.
- Capture browser proof for `/processes` or document an explicit blocker.
