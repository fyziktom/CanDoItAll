# 01b Score Geometry Driver

## Status

- Ready after `01a-common-drivers-helpers-and-ef-guardrails`.
- Critical foundation for recall, attention, belief, salience, replay, probing, answer gating, Epistemic Drive, and cross-project promotion.

## Objective

Implement the generic score geometry foundation: typed score spaces, reusable dimensions, vector snapshots, shapes/regions, scalar projection policy, evaluation traces, deterministic fakes, and validation helpers.

## Covered Inputs

- Score geometry review request in `inputs/06-score-geometry-review-request.md`.
- Score geometry findings in `analysis/08-score-geometry-architecture-review.md`.
- Requirements FR-006, FR-026, FR-046, FR-048, FR-051, FR-053, FR-054, NFR-014, NFR-027, NFR-034, NFR-035, and NFR-036.

## Prerequisites

- `01-module-foundation` must define module registration, durable identity types, policy surfaces, and initial EF conventions.
- `01a-common-drivers-helpers-and-ef-guardrails` must provide typed ids, serialization policy, deterministic fakes, budget helpers, EF query/index policy, and performance guardrails.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\inputs\06-score-geometry-review-request.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\analysis\08-score-geometry-architecture-review.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\26-score-geometry-driver.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.ScoringContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\05-recall-orchestrator.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\18-cognitive-workspace-and-attention-router.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\19-prediction-error-salience-signal-ledger.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\24-metamemory-confidence-and-abstention.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\test-and-quality-plan.md

## Deliverables

- Score-space registry contracts and implementations.
- Score geometry driver for vector/shape evaluation.
- Typed score dimension and shape definitions for the initial score spaces.
- Deterministic fake score geometry driver for tests.
- Persistence model for score evaluation traces and query-critical score components.
- Scalar projection policy for display, sorting, and queue ordering.
- Contract/model tests that reject behavior-affecting scalar-only scoring.

## Dependency Impact

- `14-neuro-foundation-claim-evidence-ledger` uses score geometry for belief vectors.
- `15-cognitive-workspace-attention-router` uses it for workspace focus, inhibition, and routing decisions.
- `16-prediction-error-salience-signals` uses it for signal vector schema and salience consumers.
- `05-recall-orchestrator` uses it for candidate ranking and trace explanations.
- `17-temporal-replay-scheduler` uses it for replay priority.
- `19-metamemory-abstention-calibration` uses it for answer-gate confidence and abstention envelopes.
- `12-epistemic-drive-engine` backs `KnowledgeNeedVector` with generic vector/shape traces.
- `10-cross-project-memory` uses it for similarity-with-separation and promotion eligibility.

## Validation Depth

- Unit tests for score-space definition lookup, missing-dimension policy, shape evaluation, scalar projection, and deterministic fake behavior.
- Persistence tests for score component indexes, no-tracking read paths, bounded lists, and schema-version filtering.
- Golden Docker context-boundary fixture proving high semantic similarity can still be inhibited by context-separation shape.
- Contract tests rejecting `FinalScore`, untyped `ScoreBreakdown`, scalar-only `Priority`, and `Dictionary<string,double>` as behavior-affecting scoring contracts.
- Performance review for hot score evaluation paths to avoid dictionary-heavy loops and repeated vector array copies.

## Implementation Steps

1. Define score-space, dimension, vector, shape, scalar projection, and evaluation trace contracts.
2. Add initial score-space definitions for recall, attention, belief, salience, replay, probe assessment, answer gate, Epistemic need, cross-project promotion, procedure maturity, mindmap similarity, and activation.
3. Add persistence model for score traces and score components.
4. Add deterministic fake driver and test builders.
5. Add scalar projection policy that requires vector/shape trace evidence before display or queue scores are emitted.
6. Update downstream contract tests to consume score geometry instead of local scoring formulas.

## Scope Exceptions

- Do not tune final production weights or thresholds in this subbundle.
- Do not implement recall, attention routing, replay scheduling, probing, answer gating, Epistemic Drive, or cross-project promotion behavior here.
- Do not implement UI visualizations beyond contracts/test fixtures.

## Do Not Do

- Do not create a generic "score everything" service that owns memory policy.
- Do not allow behavior-affecting `Dictionary<string,double>` score breakdowns.
- Do not store only scalar scores for recall rank, attention choice, belief state, replay priority, answer confidence, or cross-project promotion.
- Do not treat Qdrant similarity as a final score.
- Do not hide query-critical score dimensions only inside JSON.

## Acceptance Checklist

- Score spaces and dimensions are strongly typed and versioned.
- Score vectors preserve normalized components, confidence, evidence refs, schema version, profile, algorithm version, and input hash.
- Shapes/regions are available for context boundaries, abstention envelopes, replay urgency, promotion eligibility, and Epistemic weak regions.
- Scalar projections are optional, derived, and marked as display/sorting/queue data.
- Missing required dimensions are explicit and testable.
- Downstream subbundles cannot proceed with scalar-only scoring contracts.

## Proof Required

- Build/test output for score geometry contracts and driver.
- EF model/index proof for score components and evaluation traces.
- Golden Docker context-boundary score fixture output.
- Analyzer/grep proof that behavior-affecting scalar-only score fields were not introduced in Cognitive Memory contracts.
- Implementation report listing initial score spaces and any deferred dimensions.

## Browser Validation Logging

- N/A for backend foundation.
- Browser proof is required later only for UI surfaces that visualize score vectors, shapes, or explanations.

## Progression Gate

- Do not proceed to neuro foundation, taxonomy/projection, attention, salience, recall, replay, probing, answer gate, Epistemic Drive, or cross-project memory until the score geometry contracts, fake driver, and scalar-only rejection tests pass.
- Reopen this subbundle if a downstream phase introduces local scoring semantics instead of a declared score space.

## Suggested Agent Prompt

Implement the generic Cognitive Memory score geometry foundation only. Provide strongly typed score spaces, vectors, shapes, scalar projections, evaluation traces, deterministic fakes, persistence/index proof, and tests that reject scalar-only behavior. Do not implement feature policy or tune final weights.

