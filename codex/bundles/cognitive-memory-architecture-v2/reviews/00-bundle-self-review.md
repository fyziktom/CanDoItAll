# Bundle Self Review

## Status

- First deep architecture repair completed at design level.

## Findings

- The original bundle had the right ambition but was too optimistic about plugging memory into existing MAF and source paths.
- The earlier live source inspection showed a real prerequisite. The supplied current code snapshot now shows MAF context contribution and source snapshot boundaries are implemented, so they must be consumed and revalidated rather than recreated.
- RAG and SemanticCompletion are useful infrastructure, but neither should become durable memory truth.
- Large-data behavior must be part of V1 design through cursors, hashes, idempotency, bounded batches, and trace budgets.
- Root subbundles and traceability are now structured around dependency gates rather than feature wish lists.
- Epistemic Drive is now modeled as evidence-driven metacognition, not random curiosity or scalar-only priority.
- Learning proposals are approval-gated and source-grounded; generated learning output remains draft until validated.

## Remaining Review Needs

- Re-run target-branch validation for the prerequisite boundaries before starting implementation, even though the supplied current code snapshot already contains them.
- Review whether the first vertical slice should include Qdrant or start with lexical/relational projection only.
- Decide whether Workbench 3D coordinates remain metadata-backed for V1 or get a dedicated schema migration later.
- Confirm whether the probing-session contract names in `InteractiveMemoryProbingContracts.cs` fit the implementation namespace conventions before coding.
- Decide whether external source approval is global policy, per project, or per learning proposal.

## Interactive Probing Update Review

- The supplied code snapshot shows that the MAF context contribution boundary and source snapshot providers are already implemented. The architecture has been updated to consume those boundaries rather than re-open them.
- The previous Epistemic Drive design mentioned probing, but did not define a full probing subsystem. This update adds a dedicated Interactive Memory Probing architecture, contracts, diagrams, subbundle, validation matrix, and Codex prompt.
- The most important new invariant is: probe feedback is evidence, not direct truth mutation.
- The new regression/calibration loop is necessary because probing should create repeatable tests and confidence calibration data, not just one-off chat transcripts.
- Remaining implementation decision: whether the first UI ships as a simple Blazor split panel or as a richer Canvas/graph-assisted workbench. Backend contracts should not depend on that choice.
