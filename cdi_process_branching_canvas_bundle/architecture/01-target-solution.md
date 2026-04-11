# Target Solution

## Target Outcome

- The process canvas gains a new optional advanced workbench node shape that can expose multiple named inputs and outputs with independent curve anchors.
- Process branching is represented by its own branch node on the canvas, visually connected to the originating process step, instead of being hidden as metadata or footer text.
- Branch nodes render connectable output ports for each explicit matched outcome plus a `Default` route and an `Error` route.
- Decision-maker branches can also render an incoming role-definition connection so the decision authority is visible on the canvas.
- Connector authoring starts with left click on an explicit connector circle and finishes with left click on a specific target circle.
- Connector circles render directly on the badges that name the corresponding input or output.
- Canvas edits that move role, router, or other derived nodes survive rerender-triggering interactions and reloads through a canonical persisted path.

## Boundary Decisions

- Shared multi-port rendering and port-aware link geometry belong in `CanDoItAll.Components.CanvasLib`.
- Process-specific interpretation of branch outcomes, decision roles, branch-node creation, and canonical persistence belong in `CanDoItAll.Modules.Processes`.
- Existing legacy workbench nodes and links must remain supported and unchanged by default.
- The branch-node canvas projection may remain additive and derived only if execution proves that the current process model can still represent the required semantics honestly.
- UI-only many-to-many drawings without canonical persisted meaning are forbidden for this scope.

## Proposed Additive Model

- Introduce or keep an optional advanced node contract with explicit input and output port collections, stable port identifiers, and display metadata needed for labels and layout.
- Extend workbench links additively so a link may target a specific source port and target port when the source or target node supports advanced ports.
- Keep the old `SourceId` and `TargetId` path functional so legacy nodes and consumers remain untouched.
- Update workbench rendering, hit testing, and connector overlays to prefer named-port geometry when present and fall back to current whole-node anchors otherwise.
- Derive connector-circle placement from the actual badge geometry or from additive badge-level layout metadata, not from generic evenly spaced edge slots.
- Treat connector authoring as an explicit connector-circle interaction so left click on the node body can remain reserved for selection or dragging.

## Process Projection Strategy

- Keep process steps as their existing node type when that is still the best representation.
- Project a separate branch node when the user adds or edits branching for a step.
- Connect the original step to the branch node with a normal flow edge so the source of the branch remains explicit.
- Project one output port per branch outcome, plus additive default and error ports, and map downstream `DependsOnBranchOutcomeId` steps to those ports.
- Project a role-definition input port when a branch uses `DecisionRoleRequirementId`.
- Stop treating every target step as if it can only have one upstream dependency if the current scope requires join-style inputs. If the current model is insufficient, introduce an additive strongly typed connection representation instead of overwriting a single dependency field.

## Persistence Strategy

- Persisted node positions for derived nodes must round-trip through a canonical module-owned storage path, not only browser memory or transient component fields.
- The persistence proof must include at least one move, one later interaction that rebuilds or reselects the surface, and one re-read of the stored state.

## Expected Architecture Troubles To Track

- Whether the current process data model can represent default and error routes without inventing weak magic-string conventions.
- Whether branch-node identity should remain derived or needs a persisted wrapper once authoring becomes richer.
- Whether many-to-many joins require a new explicit dependency-edge model.
- Whether the current selection panel and editor orchestration can create and edit branch nodes without introducing side effects in component lifecycle hooks.
- Whether canvas auto-layout or manual placement rules need additive metadata to keep multi-port nodes readable.
- Whether derived-node layout persistence belongs in the process definition itself or a canonical module-owned UI-state store.
