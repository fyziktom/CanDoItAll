# Target Solution

## Intended End State

- The process module owns a strongly-typed inventory of node families, port families, step-kind applicability rules, and connection cardinality.
- `process-step`, `process-role`, and `process-branch-router` all project their editable semantics as visible advanced-node ports instead of relying on generic node-body anchors.
- The process service layer persists every relationship the canvas claims to edit.
- Runtime projection mirrors the authored graph strongly enough to make the executed process understandable.
- The canvas becomes the primary editing surface for the main process graph, with forms serving as secondary detail editors rather than the only place where the graph can be defined.

## Architectural Boundaries

- CanvasLib remains generic.
  - It handles advanced-node rendering, anchor geometry, port hit-testing, and connection gestures.
  - It does not learn process-specific responsibility kinds or artifact semantics.
- `CanDoItAll.Modules.Processes` owns business meaning.
  - Port IDs, port groups, step-kind applicability, canonical relationship mapping, and persistence updates live here.
- Persistence changes must stay canonical.
  - If the canvas can draw a relation, the service and database must be able to save and reload it.

## Recommended Design Shape

1. Introduce a process-canvas port catalog.
   - One place that defines:
     - node families
     - port IDs
     - port labels
     - applicable step kinds
     - allowed source and target combinations
     - cardinality
     - whether the relation is system-managed or user-authored

2. Extend canonical persistence where the model is missing.
   - Keep using existing entities for:
     - step dependencies
     - routed dependencies
     - role assignments
     - decision authority
   - Add an explicit artifact-consumption relation if artifact-input authoring is required for honest canvas-first editing.

3. Upgrade the process step and role projections to advanced multi-port nodes.
   - Step nodes get visible structural, participant, and artifact contract ports.
   - Role nodes get visible responsibility outputs and decision-authority output.
   - Branch routers remain additive derived nodes that plug into the same contract.

4. Generalize connection dispatch in `ProcessWorkspace.Canvas.cs`.
   - Replace branch-special-case routing with port-family-aware create and delete handling.
   - Keep system-managed connections explicit and protected where required.

5. Bring runtime projection into readable parity.
   - Prefer a curated read-only parity model over a rushed runtime editor.
   - Preserve the same semantic vocabulary so authored processes remain legible after execution starts.

## Known Hard Problem

- Artifact expectations are not the same as artifact-consumption links.
- If the initiative stops at showing artifact-output badges without a persisted consumer relation, the canvas will still not be primary for that part of the graph.
- This bundle therefore treats artifact consumption as a canonical-foundation decision, not a late cosmetic enhancement.
