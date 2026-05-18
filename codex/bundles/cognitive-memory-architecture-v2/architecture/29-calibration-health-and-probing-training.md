# 29 Calibration Health And Probing Training

## Purpose

Extend current probing confidence calibration into a durable self-regulation training loop.

The system should learn not only what is true, but when it should trust itself, ask, audit, probe, review, escalate, or abstain.

## Calibration Event

Every probe answer, important recall answer, workflow/tool outcome, professor review, human review, and user correction should create or update calibration evidence when feasible.

Required data:

- predicted confidence projection,
- self-regulation assessment trace,
- answer-gate trace,
- answer posture,
- domain/knowledge region,
- task type,
- model profile,
- retrieval profile,
- source sufficiency profile,
- risk category,
- known failure pattern matches,
- outcome classification,
- actual correctness score where measurable,
- correction type,
- created regression/probe/review/replay links.

## Calibration Aggregates

Create aggregates by:

- domain,
- task type,
- model profile,
- risk category,
- feature pattern.

Suggested metrics:

- sample count,
- expected calibration error,
- Brier score or squared calibration loss,
- signed confidence bias,
- overconfidence rate,
- underconfidence rate,
- abstention precision,
- abstention false-positive rate,
- wrong-scope recurrence,
- missing-source recurrence,
- professor-review disagreement rate,
- human-review rejection rate.

## Binning

Calibration health must use bins instead of one average:

| Predicted confidence | Expected meaning |
|---|---|
| `0.00-0.20` | Usually ask, probe, audit, or abstain. |
| `0.20-0.40` | Hypothesis or heavily caveated answer. |
| `0.40-0.60` | Useful but uncertain answer. |
| `0.60-0.80` | Direct with caveats unless risk is high. |
| `0.80-1.00` | Direct only when evidence and calibration support it. |

The bin is display/analysis data. Behavior still uses score geometry, posture policy, and evidence.

## Probing As Training

Probing sessions should deliberately exercise:

- strong domains,
- weak domains,
- recently corrected patterns,
- high-use project areas,
- known wrong-scope boundaries,
- source-poor regions,
- stale procedures,
- professor-reviewed topics,
- high-value project decisions.

The goal is to train both memory content and confidence calibration.

## Calibration Profile Changes

Calibration updates must be versioned:

```text
calibration events
  -> aggregate health metrics
  -> proposed profile threshold/shape update
  -> review or policy approval where needed
  -> new profile version
  -> old traces remain interpreted by old profile
```

No single correction should permanently define incompetence. No single success should permanently define competence. Use evidence accumulation, recency windows, risk weighting, and review policy.

## Overconfidence Handling

Repeated overconfidence should:

- lower allowed posture for the feature pattern,
- require more source anchors,
- add or update known failure patterns,
- create regression tests,
- schedule probing drills,
- trigger professor challenge for high-impact cases,
- publish salience and prediction-error evidence.

## Underconfidence Handling

Repeated underconfidence should:

- add reinforcement evidence,
- allow stronger posture for scoped low-risk cases,
- preserve source requirements,
- preserve contradiction dimensions,
- avoid suppressing review gates for high-risk cases.
