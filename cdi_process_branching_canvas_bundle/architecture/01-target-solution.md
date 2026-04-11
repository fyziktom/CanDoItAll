# Target Solution

## Target Outcome

- The process canvas gains a new optional advanced workbench node shape that can expose multiple named inputs and outputs with independent curve anchors.
- Process branching is represented by its own branch node on the canvas, visually connected to the originating process step, instead of being hidden as metadata or footer text.
- Branch nodes render connectable output ports for each explicit matched outcome plus a `Default` route and an `Error` route.
- Decision-maker branches can also render an incoming role-definition connection so the decision authority is visible on the canvas.

## Boundary Decisions

- Shared multi-port rendering and port-aware link geometry belong in `CanDoItAll.Components.CanvasLib`.
- Process-specific interpretation of branch outcomes, decision roles, and branch-node creation belongs in `CanDoItAll.Modules.Processes`.
- Existing legacy workbench nodes and links must remain supported and unchanged by default.
- The branch-node canvas projection may be additive and derived from current process models; do not force a broad process-domain rewrite unless execution proves that the existing model cannot represent the required semantics.

## Proposed Additive Model

- Introduce an optional advanced node contract with explicit input and output port collections, stable port identifiers, and display metadata needed for labels and layout.
- Extend workbench links additively so a link may target a specific source port and target port when the source or target node supports advanced ports.
- Keep the old `SourceId` and `TargetId` path functional so legacy nodes and consumers remain untouched.
- Update workbench rendering, hit testing, and connector overlays to prefer named-port geometry when present and fall back to current whole-node anchors otherwise.

## Process Projection Strategy

- Keep process steps as their existing node type when that is still the best representation.
- Project a separate branch node when the user adds or edits branching for a step.
- Connect the original step to the branch node with a normal flow edge so the source of the branch remains explicit.
- Project one output port per branch outcome, plus additive default and error ports, and map downstream `DependsOnBranchOutcomeId` steps to those ports.
- Project a role-definition input port when a branch uses `DecisionRoleRequirementId`.

## Expected Architecture Troubles To Track

- Whether the current process data model can represent default and error routes without inventing weak magic-string conventions.
- Whether branch-node identity should remain derived or needs a persisted wrapper once authoring becomes richer.
- Whether the current selection panel and editor orchestration can create and edit branch nodes without introducing side effects in component lifecycle hooks.
- Whether canvas auto-layout or manual placement rules need additive metadata to keep multi-port nodes readable.
