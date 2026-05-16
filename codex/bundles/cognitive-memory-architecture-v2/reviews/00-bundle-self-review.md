# Bundle Self Review

## Status

- First deep architecture repair completed at design level.

## Findings

- The original bundle had the right ambition but was too optimistic about plugging memory into existing MAF and source paths.
- The earlier live source inspection showed a real prerequisite. The supplied current code snapshot now shows MAF context contribution and source snapshot boundaries are implemented, so they must be consumed and revalidated rather than recreated.
- RAG and SemanticCompletion are useful infrastructure, but neither should become durable memory truth.
- Large-data behavior must be part of V1 design through cursors, hashes, idempotency, bounded batches, and trace budgets.
- Root subbundles and traceability are now structured around dependency gates rather than feature wish lists.
- The original v2 phase order still let complex features depend on ad hoc fakes and persistence conventions. A new common driver/helper/EF guardrail phase now closes that gap before downstream implementation starts.
- The data model previously allowed too much query-relevant information to hide inside JSON payloads. The architecture now requires relational/indexed state for source refs, evidence refs, candidates, projections, review, proposal, probe, and regression links.
- The probing workbench was too large as a single phase. It is now split into backend probing core/regression/calibration first, then the Dialogue Workbench UI and workflow/tool wrappers.
- Epistemic Drive and distributed compute were too early in the old order. They now wait until project-scoped recall, consolidation, review, MAF integration, and probing evidence are stable enough to consume safely.
- The neuro-cognitive patch exposed a deeper issue: claim/evidence/context/mutation authority, workspace/attention, signal ledgers, replay, procedure skill maturity, and answer gating affect earlier phases. They are now prerequisites rather than late appendix work.
- The scoring review exposed a cross-cutting flaw: recall, attention, belief, replay, probing, answer confidence, and cross-project promotion could still regress to local weighted-sum scoring. A new score geometry foundation now closes that before downstream phases start.
- The execution-control review exposed a delivery flaw: markdown phase gates alone are not enough for a long multi-agent implementation. A structured workbook phase ledger is now required and must stay synchronized with the execution report.
- The original project split was too eager. The implementation plan now starts with the smallest viable module/abstraction shape and defers additional Cognitive Memory projects until dependency direction or test isolation proves they are useful.
- The first visible vertical slice previously risked becoming projection-first. It now explicitly includes evidence anchors, claims, context frames, mutation authority, score geometry, workspace focus/inhibition, and answer gating before recall is treated as architecturally valid.
- Contract sketches now make mutation authority the public write boundary, replace ordinal validation-state filtering with allowed/excluded policy filters, and replace untyped projection payloads with typed payload values.
- Epistemic Drive is now modeled as evidence-driven metacognition, not random curiosity or scalar-only priority.
- Learning proposals are approval-gated and source-grounded; generated learning output remains draft until validated.

## Remaining Review Needs

- Re-run target-branch validation for the prerequisite boundaries before starting implementation, even though the supplied current code snapshot already contains them.
- Review whether the first vertical slice should include Qdrant or start with lexical/relational projection only.
- Decide whether Workbench 3D coordinates remain metadata-backed for V1 or get a dedicated schema migration later.
- Confirm whether the probing-session contract names in `InteractiveMemoryProbingContracts.cs` fit the implementation namespace conventions before coding.
- Decide whether external source approval is global policy, per project, or per learning proposal.
- During the first implementation phase, validate the common helpers against real target-branch EF/provider patterns rather than treating them as isolated test utilities.
- Before any probe UI work, confirm that backend probe correction, regression, and calibration records have enough durable evidence to support repeatable tests.
- Before source ingestion starts, validate evidence anchor, claim, context frame, entity alias, mutation command, and audit persistence shape. If this is wrong, all downstream memory proof becomes suspect.
- Before source ingestion or recall starts, validate score-space definitions, score vector persistence, shape evaluation, scalar projection policy, and scalar-only rejection checks. If this is wrong, downstream recall/replay/learning proof becomes suspect.
- Before recall starts, validate workspace, attention, prediction error, and salience signal contracts. If recall bypasses these, the system regresses to context-pack RAG.
- Before each implementation phase starts or closes, update `checklists/cognitive-memory-implementation-control.xlsx` and confirm it matches `reviews/01-execution-report.md`.
- Before accepting correction, probing, stale refresh, or learning implementation, validate reconsolidation/revision lineage: previous claim version, evidence anchors, mutation command, audit event, review state where required, and projection invalidation.

## Score Geometry Review

- Added `architecture/26-score-geometry-driver.md`, `contracts/csharp/CognitiveMemory.ScoringContracts.cs`, `analysis/08-score-geometry-architecture-review.md`, diagram `18`, and subbundle `01b-score-geometry-driver`.
- Main correction: behavior-affecting scoring must use typed score spaces, vectors, shapes, scalar projection policy, and evaluation traces. Scalars remain display/sorting/queue projections only.
- Updated recall, mindmap similarity, claim/belief, attention, salience, replay, probe metadata, answer gate, Epistemic Drive, and cross-project promotion contracts/docs to consume score geometry.
- Remaining implementation decision: exact initial shape profiles and thresholds should be deterministic fixtures first, then tuned from regression/calibration evidence.

## Neuro-Cognitive Patch Review

- Added architecture files `17` through `24`, diagrams `14` through `17`, neuro requirements, neuro validation plan, contract sketches, and subbundles `14` through `20`.
- Preserved core v2 rules: Qdrant remains projection only, raw sources remain authoritative, probing feedback is evidence only, learning is approval-gated, distributed workers cannot mutate truth, and generated/simulated output remains draft/speculative.
- Main correction: phase order now places neuro foundation before source ingestion and recall-dependent phases.
- Remaining implementation decision: how much deterministic entity/context binding is enough for the first vertical slice before introducing heavier extraction/classification.

## Interactive Probing Update Review

- The supplied code snapshot shows that the MAF context contribution boundary and source snapshot providers are already implemented. The architecture has been updated to consume those boundaries rather than re-open them.
- The previous Epistemic Drive design mentioned probing, but did not define a full probing subsystem. This update adds a dedicated Interactive Memory Probing architecture, contracts, diagrams, subbundle, validation matrix, and Codex prompt.
- The most important new invariant is: probe feedback is evidence, not direct truth mutation.
- The new regression/calibration loop is necessary because probing should create repeatable tests and confidence calibration data, not just one-off chat transcripts.
- Remaining implementation decision: whether the first UI ships as a simple Blazor split panel or as a richer Canvas/graph-assisted workbench. Backend contracts should not depend on that choice.
