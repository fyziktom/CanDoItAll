# 26 Score Geometry Driver

## Purpose

Provide one reusable scoring foundation for Cognitive Memory instead of many local weighted-sum formulas.

The module needs scoring for recall, attention, belief, salience, replay, probing, answer gating, Self-Regulation, Epistemic Drive, cross-project promotion, and UI sorting. These are related but not identical. The shared layer should provide the mechanics for typed score spaces, vectors, shapes, normalization, scalar projections, and traceability. Feature services still own policy decisions.

## Problem

The older architecture used simple score arithmetic:

```text
final = semantic + lexical + graph + activation + confidence - staleness
```

That is too weak for cognitive memory because it hides why a memory was selected, why a related memory was inhibited, and why a confident answer should still abstain. It also makes future tuning fragile: changing one weight can silently affect recall, replay, probing, and learning in incompatible ways.

## Core Model

Use a generic score geometry model:

```text
score space definition
  + typed dimensions
  + normalized vector snapshots
  + optional shapes/regions/boundaries
  + evidence refs
  + evaluation policy
  -> score evaluation trace
  -> optional display projection
```

The score vector and evaluation trace are the durable decision evidence. A scalar display score is allowed only for UI sorting, queue ordering, or operator summaries.

## Score Space Definition

Each score space defines:

- score space kind,
- schema version,
- dimension definitions,
- normalization profile,
- missing-dimension policy,
- shape interpretation,
- scalar projection policy,
- evidence requirements,
- algorithm version.

Score spaces must be strongly typed. Do not use ad hoc string keys for behavior-affecting dimensions.

## Dimensions

Reusable dimensions include:

- semantic similarity,
- lexical match,
- graph proximity,
- spatial proximity,
- temporal recency,
- context fit,
- source sufficiency,
- source quality,
- evidence support,
- evidence attack,
- contradiction pressure,
- staleness pressure,
- calibration risk,
- policy/access risk,
- redaction pressure,
- risk impact,
- usefulness,
- reward,
- rework cost,
- user interest,
- strategic alignment,
- procedure maturity,
- expected effort,
- expected reuse,
- cognitive load,
- evidence strength,
- evidence coverage,
- source reliability,
- recency fit,
- novelty risk,
- consequence risk,
- model uncertainty,
- historical calibration fit,
- domain competence fit,
- known failure pattern similarity,
- scope ambiguity,
- user correction pressure,
- self-model stability,
- professor review value,
- escalation cost,
- abstention cost,
- confidence bias,
- overconfidence rate,
- underconfidence rate,
- human review agreement,
- professor review agreement,
- humility trigger pressure,
- confidence reinforcement pressure.

Not every score space uses every dimension. Missing dimensions must be explicit: unavailable, not applicable, or blocked by policy.

## Shapes And Regions

A score shape represents a decision region in a space, not just a number.

Examples:

- recall focus region for the current task,
- context-boundary separation shape for production/test Docker,
- abstention envelope for source-poor high-risk answers,
- replay urgency region for high-risk stale procedures,
- cross-project promotion region where similarity is high and privacy/context separation is low,
- Epistemic Drive Pareto front or weak-region shape.

Supported shape kinds should include:

- point vector,
- weighted region,
- centroid/radius,
- threshold envelope,
- boundary plane,
- Pareto frontier,
- trajectory over time.

## Evaluation Trace

Every behavior-affecting evaluation should persist or attach a trace with:

- score space kind and schema version,
- input vector snapshots,
- matched shapes or boundaries,
- missing dimensions and reasons,
- normalization profile,
- evidence refs,
- scalar projection if one was produced,
- selected/inhibited/abstained decision reason,
- algorithm version,
- timestamp.

Recall and probing traces should reference score evaluation ids rather than copying only final score numbers.

## Relationship To Existing Components

### Recall

Recall candidate ranking must evaluate a recall score space. Semantic, lexical, graph, spatial, activation, source, belief, context, staleness, contradiction, and access signals remain separate dimensions.

### Attention

Attention routing must evaluate an attention score space. It should compare the current workspace against operation shapes such as recall, answer-from-workspace, clarification, source audit, probe, review, replay, learning request, or abstention.

### Belief

Belief state must evaluate a belief score space over support evidence, attack evidence, source quality, context validity, temporal validity, review state, and contradiction pressure. The belief state is not a sum of support minus attack.

### Salience

Salience signals are vector components. Consumers can project them differently. Replay may prioritize risk/staleness; Epistemic Drive may care about source weakness/user interest; answer gating may care about calibration risk.

### Replay

Replay priority must evaluate a replay score space and store the vector/shape trace. A scalar queue priority may be cached, but it is derived.

### Probing

Probe findings and regression value must use the same score geometry contracts. Probe answers should not carry an untyped score breakdown dictionary.

### Metamemory Answer Gate

Answer confidence must be an answer-gate evaluation trace. Display confidence is a rendering aid, not the decision model.

### Self-Regulation

Self-regulation assessment, self-model competence, calibration health, professor-review routing, and answer posture selection must use declared score spaces. Examples:

- `SelfRegulationAssessment` evaluates evidence strength, evidence coverage, source reliability, context fit, contradiction pressure, novelty/consequence risk, historical calibration fit, domain competence fit, known failure pattern similarity, scope ambiguity, access/redaction pressure, cognitive load, and model uncertainty.
- `SelfModelCompetence` evaluates source coverage, probe success, regression success, human review agreement, workflow validation, correction pressure, confidence bias, and profile stability.
- `CalibrationHealth` evaluates overconfidence, underconfidence, Brier/squared loss, expected calibration error or equivalent, abstention quality, wrong-scope recurrence, source-insufficient recurrence, and drift.
- `ProfessorReviewRouting` evaluates review value, consequence risk, novelty, weak competence, contradiction pressure, source sufficiency, escalation cost, access/redaction pressure, and expected learning value.
- `AnswerPosture` evaluates the assessment result against posture shapes such as direct confident, caveated, hypothesis, clarification, source audit, probe, review, professor review, or abstain.

Scalar display confidence is a projection only. It cannot select posture by itself.

### Epistemic Drive

`KnowledgeNeedVector` remains domain-specific, but it must be backed by a generic score vector snapshot and region/shape evaluation so the same driver can normalize, compare, and trace dimensions.

### Cross-Project Memory

Cross-project promotion must compare similarity and separation dimensions together. High semantic similarity is insufficient when context, source policy, entity identity, or privacy dimensions disagree.

## Persistence Shape

Store high-value score evidence in two layers:

1. Relational columns/tables for query-critical facts:
   - score space kind,
   - schema version,
   - owner record id,
   - dimension kind,
   - normalized value,
   - confidence,
   - evidence id,
   - calculated timestamp,
   - algorithm version.
2. Bounded versioned payload for full vector/shape snapshots:
   - all dimensions,
   - shape parameters,
   - scalar projection,
   - explanation text,
   - metadata hash.

Do not hide query-critical dimensions only in JSON.

## EF And Performance Requirements

- Score component queries are paged and filtered by owner, project, score space kind, dimension kind, and calculated time.
- Read paths use no-tracking DTO projections.
- Hot evaluations avoid allocating dictionaries and repeated vector arrays.
- Score-space definitions are cached by schema version.
- Shape evaluation must be deterministic for a fixed input vector and profile.
- Large evaluation reports use storage references with hashes.

## Validation Requirements

- Contract tests reject scalar-only behavior for recall, attention, belief, replay, probing, answer gate, Epistemic Drive, and cross-project promotion.
- Contract tests reject scalar-only behavior for self-regulation assessment, self-model competence, calibration health, professor-review routing, and answer posture.
- Golden Docker fixtures prove high semantic similarity can still be inhibited by a context-boundary shape.
- Score geometry tests prove missing dimensions are explicit and do not silently default to neutral when required.
- Scalar projections are reproducible from vector/shape traces.
- Changing a score schema version does not reinterpret old traces without migration or recalculation.
