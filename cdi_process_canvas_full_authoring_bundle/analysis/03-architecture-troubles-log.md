# Architecture Troubles Log

## Confirmed Gaps

1. `process-step` is still a generic anchor node.
   - The process module has canonical step semantics, but the canvas projection still treats a step as a mostly single-anchor surface.
   - This blocks visible participant-role, artifact, and structural port authoring.

2. `process-role` is only partially port-aware.
   - The role node already exposes a decision-authority output.
   - It does not expose the canonical responsibility kinds that already exist in the model.

3. Artifact expectations are owned by a step but artifact consumption is not a canonical graph relation.
   - The model can describe what a step produces or expects.
   - The model does not currently describe that step `B` consumes artifact `X` produced by step `A`.
   - Full canvas-first authoring likely requires an explicit model extension here.

4. The process-canvas connection layer is still branch-special-case logic.
   - Current authoring logic understands direct step dependencies, routed dependencies, and decision-authority assignment.
   - It does not yet provide a general port-family dispatch for all future node families.

5. Runtime projection is narrower than the likely definition-canvas end state.
   - Even if definition authoring becomes rich and explicit, runtime nodes will remain harder to read unless they project the same port families or a principled read-only subset.

## Architectural Decisions To Preserve

- Keep CanvasLib additive.
  - Advanced nodes remain optional.
  - Legacy canvases must not be rewritten wholesale.
- Keep process semantics in the process module.
  - CanvasLib should not learn business meanings such as `Approver` or `Artifact input`.
- Prefer strongly-typed port catalogs.
  - Do not let node-family semantics devolve into ad hoc string comparisons scattered through `ProcessWorkspace.Canvas.cs`.

## Required Decisions During Execution

1. Whether artifact-consumption links become first-class canonical entities in this initiative.
   - Recommended answer: yes, if the literal `full possibility to edit all processes via canvas primarily` claim is to be honest.

2. Whether runtime projection gets full parity or a curated read-only projection.
   - Recommended answer: curated parity that preserves authored meaning without forcing runtime editing if runtime editing is not part of the user request.

3. Whether step-kind rules are encoded as explicit applicability rules in the port catalog.
   - Recommended answer: yes, at least for obvious structural rules such as `Start` having no upstream input and `End` having no downstream output.

## Non-Negotiable Honesty Rules

- Do not draw artifact links that the service layer cannot save.
- Do not silently fall back from typed ports to generic node-body connections when a port should exist.
- Do not claim the canvas is primary if role assignment, artifact routing, or step routing still require forms for normal use.
