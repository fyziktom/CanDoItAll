# Architecture Troubles Log

## Confirmed At Preparation Time

- The process domain already tracks branch outcomes and decision-role ownership, but the canvas projection still collapses those semantics into plain whole-node links.
- The shared workbench contract does not yet expose stable named ports, which means the renderer cannot express one curve per outcome plus default and error.
- The current process workspace authoring flow already knows about branch outcomes, but it does not project them as first-class branch nodes on the canvas.
- The bundle must verify whether default and error routes can be represented without inventing weak magic strings in the domain.

## Watch During Execution

- Whether role-definition nodes are already projected cleanly enough to support visible decision-role input routing.
- Whether branch-node identity can remain derived or needs persisted placement metadata.
- Whether multi-port node density requires additive layout rules to stay readable.

## Confirmed During Execution

- The current process definition model still allows only one `DependsOnStepId` plus one optional `DependsOnBranchOutcomeId` per step. That is enough for explicit fan-out routing, but it is not enough for true cyclic review loops or multi-parent joins. The seeded software-development scenario therefore uses a branch-heavy rehearsal instead of a real loop-back edge into the same decision node.
- Branch router nodes and role nodes are still derived canvas projections rather than persisted domain entities. The additive UI-state manual-position support keeps them usable for authoring, but it is not yet a canonical shared layout contract across users, exports, or later replay tooling.
- The additive multi-port renderer stays readable at large-screen width, but `1280x800` is already near the density limit for this branch-heavy scenario. If the process library starts using more than one large router in a single scene, the workspace will need stronger layout or grouping rules instead of relying on fit-to-view alone.

## Update Rule

- Add each newly discovered architecture issue here with the affected files, the symptom, and whether it reopens an earlier subbundle.
