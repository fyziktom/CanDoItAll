# Score Geometry Architecture Review

## Finding Summary

The v2 bundle already rejects scalar-only scoring for Epistemic Drive and salience, but that rule is not applied consistently across the rest of the architecture. Several surfaces still describe the old model as weighted sums, isolated sub-scores, or untyped score dictionaries.

## Scalar Leakage Found

| Area | Current risk | Required repair |
|---|---|---|
| Recall ranking | `SemanticScore`, `LexicalScore`, `GraphScore`, `ActivationScore`, `ConfidenceScore`, and `FinalScore` imply a flat additive formula. | Recall candidates must carry a score vector, matched shapes/boundaries, and a score evaluation trace. A scalar rank may exist only as derived display data. |
| Mindmap clustering | The hybrid formula mixes semantic, graph, spatial, metadata, activation, and source confidence into one score. | Mindmap similarity must keep separate semantic/spatial/graph/metadata/temporal dimensions and classify clusters or context boundaries through score-space shapes. |
| Attention routing | `ScoreBreakdown` as `Dictionary<string,double>` is untyped and encourages local scoring. | Attention routing must use a declared score space with typed dimensions and an evaluation trace. |
| Belief state | Support, attack, source quality, context fit, and staleness are isolated doubles. | Belief state must be a shape/vector evaluation over evidence dimensions, with support/attack as dimensions instead of final truth. |
| Cognitive signals | Individual signal magnitude is useful, but downstream consumers can treat it as a scalar priority. | Signals must publish score vector components with schema/version/evidence metadata. |
| Replay scheduling | `Priority` is a single scalar. | Replay priority must be derived from a replay score vector and replay-shape match, not stored as the only decision basis. |
| Probe metadata | Probe answer score breakdown is stringly typed. | Probe assessment must use the same score geometry driver as recall and answer gating. |
| Cross-project memory | Similarity-with-separation scoring is not defined precisely enough. | Cross-project promotion must compare both similarity and separation shapes before proposing merge/promotion. |
| Memory item confidence/activation | The data model stores confidence and activation as simple numbers. | Store score vector snapshots and derived display projections separately. |

## Required Architecture Change

Add a generic Score Geometry Driver as a shared foundation. It should own:

- score-space definitions,
- typed dimension definitions,
- vector snapshots,
- region/shape definitions,
- normalization profiles,
- missing-dimension policy,
- scalar projection policy,
- score evaluation traces,
- deterministic test fixtures.

The driver does not own memory policy. It evaluates score spaces for consumers such as recall, attention, belief, replay, probing, answer gating, and Epistemic Drive. Each consumer declares its own score space and policy constraints.

## Core Principle

The authoritative model is:

```text
evidence + typed dimensions + score space schema + normalization profile + shapes/boundaries
  -> evaluation trace
  -> optional scalar projection for display/sorting
```

The scalar projection is never the stored truth or the only explanation.

## Implementation Consequences

- Add subbundle `01b-score-geometry-driver` after common helpers and before claim/evidence, projection, workspace, salience, recall, replay, probing, answer gate, Epistemic Drive, and cross-project memory.
- Update C# contract sketches so public contracts cannot require scalar-only scoring.
- Update data model docs so high-volume score components are relational/indexable where queried, while full vector snapshots can be stored as bounded versioned payloads with hashes.
- Update validation so tests reject scalar-only recall, attention, belief, replay, probing, cross-project, salience, and Epistemic Drive implementations.
- Keep Qdrant similarity scores as external projection signals only. They become one dimension in a score vector, not the final memory rank.

## Specific Score Spaces To Define

| Score space | Core dimensions |
|---|---|
| Recall candidate | semantic similarity, lexical match, graph proximity, spatial proximity, context fit, source sufficiency, activation, belief support, contradiction pressure, staleness, access/redaction pressure, workspace focus fit. |
| Attention routing | source sufficiency, context ambiguity, cognitive load, risk, available workspace evidence, missing knowledge, calibration risk, action cost, expected value. |
| Belief state | support evidence, attack evidence, source quality, context validity, temporal validity, human validation, contradiction pressure, staleness. |
| Salience signal | novelty, surprise, risk, usefulness, reward, rework cost, contradiction pressure, user interest, strategic alignment, staleness pressure, source weakness, calibration risk. |
| Replay priority | prediction error magnitude, risk, staleness, usefulness, recurrence, procedure maturity, source trust change, strategic alignment, regression failure. |
| Probe assessment | answer correctness, source sufficiency, wrong-scope pressure, calibration risk, missing knowledge, contradiction, redaction limit, regression value. |
| Answer gate | source sufficiency, context fit, belief state, contradiction risk, staleness, redaction, calibration, risk, procedure maturity, access policy. |
| Cross-project promotion | semantic similarity, entity equivalence, context separation, source reuse permission, policy compatibility, evidence strength, privacy risk, global reuse value. |
| Epistemic need | existing `KnowledgeNeedVector` dimensions, backed by generic score vector snapshots and region shapes. |

## Reopen Triggers

Reopen this architecture slice if implementation introduces:

- a new `FinalScore`, `Priority`, `Weight`, `ScoreBreakdown`, or `Dictionary<string,double>` that affects behavior without a declared score space,
- Qdrant similarity used as final recall rank without context/source/belief dimensions,
- replay or cross-project promotion driven by one scalar,
- answer confidence that is not traceable to score dimensions and evidence,
- tests that assert only a final score instead of vector dimensions, shape match, and trace explanations.

