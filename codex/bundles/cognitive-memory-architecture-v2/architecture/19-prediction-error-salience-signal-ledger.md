# 19 Prediction Error And Salience Signal Ledger

## Purpose

Unify learning signals that currently appear separately as recall failures, probing findings, workflow failures, contradiction candidates, QA failures, stale-source observations, and human corrections.

A cognitive memory system should not only store facts. It should store why some facts, procedures, or gaps became important.

## Prediction Error

Prediction error is the difference between expected and observed outcome.

Examples:

| Expected | Observed | Error kind |
|---|---|---|
| Production deployment procedure should be recalled. | Test simulation procedure was used. | Wrong scope. |
| System answered with high confidence. | User corrected answer. | Overconfident incorrect answer. |
| Workflow executor should pass tests. | It failed due to missing Docker network knowledge. | Missing procedural knowledge. |
| Source-backed answer should include citations. | Answer used generated summary only. | Source insufficiency. |
| Current procedure should still work. | New source version invalidated step. | Stale memory. |

## Prediction Expectation

Before answering or executing an important operation, the system may record a lightweight expectation:

- expected claim,
- expected source sufficiency,
- expected procedure outcome,
- expected validation result,
- expected context boundary,
- expected confidence range.

The expectation can be implicit for simple recall and explicit for high-risk workflows/procedures.

## Prediction Error Record

A prediction error record should include:

- expectation id,
- observation id,
- project id,
- related memory item ids,
- related claim ids,
- related source refs,
- related workflow/process/probe ids,
- error kind,
- magnitude,
- confidence,
- cause hypothesis,
- suggested action,
- whether human review is required.

## Cognitive Signal Ledger

The signal ledger stores durable events that influence memory behavior.

### Signal Kinds

| Signal | Meaning |
|---|---|
| `Novelty` | Something new or structurally different appeared. |
| `Surprise` | Observed outcome differed from expectation. |
| `Risk` | Memory/procedure/claim affects high-impact operation. |
| `Usefulness` | Memory helped produce a successful result. |
| `Reward` | User accepted/valued output. |
| `ReworkCost` | Mistake caused manual rework or failed workflow. |
| `ContradictionPressure` | Competing evidence exists. |
| `UserInterest` | User repeatedly asks/probes topic. |
| `StrategicAlignment` | Topic intersects active project direction. |
| `StalenessPressure` | Source or procedure may be outdated. |
| `SourceWeakness` | Evidence is sparse, low trust, redacted, or missing anchors. |
| `CalibrationRisk` | Confidence is poorly calibrated for a feature pattern. |

## Signal Vector

Do not collapse signals into a single score. Store the vector and allow different consumers to interpret it.

Consumers:

- activation engine,
- recall ranking,
- attention router,
- replay scheduler,
- Epistemic Drive,
- learning proposal service,
- procedure maturity evaluator,
- confidence calibration,
- review queue priority.

Signal vectors must use the shared `SalienceSignal` score space. The signal ledger stores typed score components with evidence, actor, timestamp, schema version, normalization profile, and algorithm version. A signal event may expose a display magnitude, but that magnitude is derived from the vector and cannot be the only persisted decision basis.

## Signal Publication Sources

| Source | Example signals |
|---|---|
| Probe feedback | overconfidence, wrong scope, missing source, user interest. |
| Workflow run | validation success/failure, rework, procedure usefulness. |
| Process run | decision impact, repeated issue, strategic alignment. |
| Consolidation | contradiction pressure, staleness, source weakness. |
| Source ingestion | novelty, schema drift, source trust changes. |
| Human review | approval, rejection, correction confidence. |
| Regression replay | persistent failure or repaired behavior. |

## Relationship To Activation

Activation should become derived state, not the only record.

```text
signals + policy + time decay + validation state + access context
  -> MemoryActivation score vector
  -> activation evaluation trace
  -> optional display activation projection
```

Activation can change over time. The signal ledger and score evaluation trace explain why.

## Relationship To Epistemic Drive

Epistemic Drive should consume signal vectors as evidence contributors. Example:

- repeated `WrongScope` prediction errors raise context-separation pressure,
- high `ReworkCost` raises risk impact,
- high `UserInterest` raises expected reuse,
- `SourceWeakness` raises source quality concern,
- `Usefulness` raises business value.

## Relationship To Replay

Replay scheduler should use signal policy:

- high surprise + high risk = immediate review/replay,
- high usefulness + stale source = scheduled source audit,
- high user interest + low confidence = probing session,
- repeated wrong scope = context-separation regression pack,
- high procedure usefulness = procedural skill reinforcement.

## Safety Rules

- Signals can affect attention and prioritization, but cannot by themselves create truth.
- High salience must not bypass access policy.
- User interest must not leak project-private content into cross-project memory.
- Reward/usefulness must not let a wrong answer become trusted.
- Prediction error records should preserve both expected and observed evidence.

## Required Updates

- Add signal records to data model.
- Add prediction error records to probing/regression/consolidation architecture.
- Add signal consumption to recall activation and Epistemic Drive docs.
- Add tests proving high salience does not override source truth or policy.
