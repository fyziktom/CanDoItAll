# Architecture Review Checklist

## Source of Truth

- [ ] Raw sources are not replaced by summaries.
- [ ] Qdrant is documented as projection only.
- [ ] Every derived item has source refs.
- [ ] Source hash/change detection is defined.

## Human-Memory Analogy

- [ ] Episodic and semantic memory are separated.
- [ ] Procedural memory is separated from facts.
- [ ] Working memory is run/task-scoped.
- [ ] Consolidation is scheduled/idle and versioned.
- [ ] Recall is staged and explainable.
- [ ] Forgetting is soft: dormant/stale/superseded, not raw deletion.

## Existing Code Fit

- [ ] Module assembly registration fits current composition pattern.
- [ ] EF configuration discovery is used.
- [ ] Existing storage drivers are reused.
- [ ] Existing process/workflow records are used as sources.
- [ ] Existing RAG driver is wrapped, not duplicated.
- [ ] Existing semantic driver is wrapped, not duplicated.
- [ ] Existing workflow executor pattern is reused.
- [ ] Existing plugin capability model is reused.

## EF And Performance

- [ ] Read-only EF queries use no-tracking reads.
- [ ] Review, trace, source, probe, proposal, and projection list views are paged.
- [ ] Query-relevant state and relationships are indexed relational data, not JSON-only payloads.
- [ ] Lazy loading is not required or enabled for Cognitive Memory query behavior.
- [ ] EF query-shape tests assert expected command counts for source scanning, projection lists, recall traces, probe turns, review queues, and consolidation batches.
- [ ] Bulk status transitions use `ExecuteUpdateAsync`/`ExecuteDeleteAsync` or explicit bounded batches; fetch-then-update loops are justified by domain logic.
- [ ] Hot-path recall/projection queries have explicit candidate/detail budgets.
- [ ] Hot-path queries have named compiled-query candidates or a recorded reason why compilation is not useful.
- [ ] Large context packs, reports, and worker outputs are stored by reference rather than as large DB payloads.
- [ ] Vector handling avoids repeated `float[]` copies outside adapter/serialization boundaries.
- [ ] JSON serialization options/converters are cached or source-generated for repeated serialization paths.
- [ ] New hot-path C# files pass the .NET performance anti-pattern scan before downstream phases depend on them.

## Contract Strength

- [ ] Mode, status, operation, stage, section, evidence, evaluator, profile, and decision values are strongly typed.
- [ ] Behavior-affecting score spaces, dimensions, shapes, scalar projections, and evaluation traces are strongly typed.
- [ ] Any remaining string/JSON protocol fields are external-boundary exceptions with schema/profile versioning.
- [ ] Public query contracts return paged results for growing collections.
- [ ] Public APIs do not expose mutable entity graphs where DTOs are sufficient.
- [ ] Common fake source, embedding, vector, and policy providers are reused by downstream tests.

## Score Geometry

- [ ] Recall ranking uses `RecallCandidate` score vectors and shape evaluations, not a final scalar formula.
- [ ] Attention routing uses `AttentionRouting` score geometry, not untyped score breakdown dictionaries.
- [ ] Belief state uses support/attack/source/context/staleness as dimensions in a belief score space.
- [ ] Salience signals, replay priority, probing assessment, answer confidence, Epistemic need, and cross-project promotion all declare score spaces.
- [ ] Scalar display/priority/confidence projections are derived from evaluation traces and cannot be the only persisted decision basis.
- [ ] Qdrant similarity is one dimension in score geometry, not final memory identity or rank.
- [ ] Missing required score dimensions are explicit and testable.

## Retrieval Quality

- [ ] Semantic similarity is not treated as identity.
- [ ] Spatial/graph separation can override naive semantic merging.
- [ ] Entity/context binding runs before semantic merge, claim promotion, recall rendering, and procedure execution.
- [ ] Context frames preserve production/test/local/CI/environment separation for Docker-like near-neighbor topics.
- [ ] Context-separated relatedness is explicit.
- [ ] Recall intent changes candidate weighting.
- [ ] Recall traces support debugging.
- [ ] Recall/probe traces include workspace frame, attention decision, score evaluation traces, selected claims, inhibited candidates, evidence anchors, and answer-gate decision when available.

## Security and Governance

- [ ] Secrets are classified/redacted before embedding.
- [ ] Access policy applies before context injection.
- [ ] High-risk procedures need review.
- [ ] Contradictions are preserved and reviewed.
- [ ] Generated summaries cannot become raw source truth.
- [ ] Authoritative memory writes flow through mutation authority with idempotency, evidence, concurrency, audit, review, and projection invalidation.
- [ ] Evidence anchors are fine-grained enough for high-risk or contested claim review.
- [ ] Simulation and analogy output remains speculative until source-backed and reviewed.
- [ ] Answer gate can warn, clarify, source-audit, probe, review, request learning, or abstain.

## Distributed Compute

- [ ] Workers cannot mutate authoritative state.
- [ ] Job input/output hashes are enforced.
- [ ] Worker identities and capabilities are tracked.
- [ ] Coordinator validates before accepting output.

## Epistemic Drive And Learning

- [ ] Knowledge need is modeled as a vector with preserved dimensions.
- [ ] Knowledge need vectors include schema/version/normalization metadata and evidence contributors.
- [ ] Scalar priority is secondary display/sorting data only.
- [ ] Gap detection cites evidence refs.
- [ ] Learning proposals explain why this topic, why now, weak subareas, project directions, sources, outputs, risks, and approval needs.
- [ ] No learning workflow reads external sources without required approval.
- [ ] Source trust classification is stored for suggested/approved sources.
- [ ] Generated learning output remains draft until QA/human validation where required.
- [ ] Learning-derived canonical records and procedures require source refs.
- [ ] Probing failures update gap evidence but do not become automatic truth.
- [ ] Prediction errors, salience signals, answer-gate abstentions, replay outcomes, and contested claims can become evidence without becoming truth.
- [ ] Stale or contradictory records do not silently overwrite validated records.
- [ ] Qdrant/search remains projection only for learning proposals and outcomes.
- [ ] Cross-project proposals do not leak project-private source content.

## UI/UX

- [ ] User can inspect why a memory was recalled.
- [ ] User can inspect selected claims, evidence anchors, inhibited candidates, and answer-gate warnings where available.
- [ ] User can correct/split/merge memory.
- [ ] User can approve/reject review items.
- [ ] Consolidation run results are visible.
- [ ] Procedure memory can become actionable workflow/process work.
- [ ] User can inspect Night Reflection opportunities, coverage maps, evidence, estimated effort, and approval actions.
- [ ] User can request probing before learning and review probing-after-learning results.

## Phase Progression

- [ ] Each subbundle closes with a generic architecture review before the next dependent phase starts.
- [ ] `checklists/cognitive-memory-implementation-control.xlsx` is current before each subbundle starts and closes.
- [ ] Workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md` agree for the active phase.
- [ ] The root `subbundles/` and mirrored `plan/subbundles/` README files are still byte-equivalent after any subbundle-plan edit.
- [ ] Blocked or reopened prerequisites stop downstream work instead of becoming residual risk notes.
- [ ] The common driver/helper/EF guardrail phase closes before source ingestion, projection, recall, consolidation, review, probing, and learning phases start.
- [ ] Score geometry closes before neuro foundation, taxonomy/projection, workspace/attention, salience, recall, replay, probing, answer gate, Epistemic Drive, cross-project, or distributed phases start.
- [ ] Neuro foundation closes before source ingestion, taxonomy/projection, recall, consolidation, probing, learning, cross-project, or distributed phases start.
- [ ] Workspace/attention and signal ledgers close before recall/probing/Epistemic Drive consume them.
- [ ] Replay/procedural skill phases close before distributed replay or workflow automation promotion starts.
- [ ] Metamemory answer gate closes before answer-rendering UI and MAF answer injection are considered complete.
- [ ] Probing backend core closes before the Dialogue Workbench UI phase starts.
- [ ] Epistemic Drive does not consume probe outcomes until the probing core has durable evidence/regression/calibration records.
- [ ] Distributed compute starts only after project-scoped recall, consolidation, review, probing, and learning proposal behavior are validated.

## Reconsolidation And Revision Lineage

- [ ] Corrections from users or probes create evidence/review/mutation commands before active claims change.
- [ ] Stale source refresh preserves old claim versions, source hashes, supersession links, and belief-state transitions.
- [ ] Learning outcomes produce draft records until source-backed validation and policy review close.
- [ ] Projection invalidation follows authoritative memory mutation and does not run as a standalone truth update.
- [ ] Soft forgetting uses dormant, stale, superseded, or retired-projection states; raw source deletion is not used as memory cleanup.
