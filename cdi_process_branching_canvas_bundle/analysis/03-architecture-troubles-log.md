# Architecture Troubles Log

## Confirmed At Preparation Time

- The process domain already tracks branch outcomes and decision-role ownership, but the canvas projection still collapses those semantics into plain whole-node links.
- The shared workbench contract does not yet expose stable named ports, which means the renderer cannot express one curve per outcome plus default and error.
- The current process workspace authoring flow already knows about branch outcomes, but it does not project them as first-class branch nodes on the canvas.
- The bundle must verify whether default and error routes can be represented without inventing weak magic strings in the domain.

## Watch During Execution

- Whether left-click connector authoring can coexist with selection and drag without accidental node moves.
- Whether role-definition nodes and router badges can expose anchor geometry derived from the actual badge rectangles instead of generic edge slots.
- Whether branch-node identity can remain derived or needs persisted placement metadata.
- Whether the current process model can be extended to true many-to-many joins without a broader runtime rewrite.
- Whether multi-port node density requires additive layout rules to stay readable.

## Confirmed During Execution

- The process domain was extended with canonical `ProcessStepDependencyDefinition` rows, so many-to-many joins are now first-class and runtime activation can wait for all required inputs. True cyclic review loops back into the same decision path are still not first-class and remain a separate architectural limit.
- Branch router nodes and role nodes are still derived canvas projections rather than separate persisted domain entities, but their positions are now written through canonical definition fields (`CanvasX`, `CanvasY`, `BranchCanvasX`, `BranchCanvasY`) instead of transient-only component UI state.
- The additive multi-port renderer stays readable at large-screen width, but `1280x800` is already near the density limit for this branch-heavy scenario. If the process library starts using more than one large router in a single scene, the workspace will need stronger layout or grouping rules instead of relying on fit-to-view alone.
- Left-click connector authoring now coexists with selection and drag: the canvas starts drafting from the clicked output circle and completes on the clicked target circle without requiring right-click initiation.
- Advanced-node anchor placement needed a row-grid correction so badge circles line up with the actual badge rows. That fix restored the missing router-side `Review lead` input circle and aligned the visible circles with their pills in live proof.
- The process workspace snap-back symptom was real. Saving and publishing originally cloned the next draft through transient lookups that lost the newly added step dependency and layout state. The clone path now carries dependency rows and persisted role/router positions forward correctly, with integration proof on save/get-editor roundtrip and publish-to-next-draft cloning.
- System `Default` and `Error` routes must exist for router semantics, but they should not be mandatory to wire. Publish validation originally rejected valid branching definitions when those synthetic routes were unconnected; that rule was corrected during execution.

## Update Rule

- Add each newly discovered architecture issue here with the affected files, the symptom, and whether it reopens an earlier subbundle.
