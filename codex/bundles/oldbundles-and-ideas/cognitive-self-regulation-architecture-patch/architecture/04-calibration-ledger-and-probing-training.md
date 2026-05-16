# Calibration Ledger And Probing Training

## Purpose

Extend current probing confidence calibration into a durable training loop for Self-Regulation.

The system should learn not only facts, but also when to trust itself.

## Calibration Event

Every probe answer, important recall answer, workflow answer, professor review, and user correction should create or update a calibration event when feasible.

Required event data:

- predicted confidence projection,
- full answer-gate and self-regulation traces,
- answer posture,
- domain/knowledge region,
- task type,
- model profile,
- retrieval profile,
- source sufficiency profile,
- risk category,
- outcome classification,
- actual score/correctness,
- correction type,
- failure pattern matches,
- created regression/probe/review links.

## Calibration Aggregates

Create aggregates by domain, task type, model profile, risk category, and feature pattern.

Suggested metrics:

- sample count,
- expected calibration error,
- Brier score or squared calibration loss,
- signed bias,
- overconfidence rate,
- underconfidence rate,
- abstention precision,
- abstention false-positive rate,
- wrong-scope recurrence,
- missing-source recurrence,
- professor-review disagreement rate,
- human-review rejection rate.

## Binning

Calibration should use bins rather than a single average.

| Predicted Confidence | Expected Meaning |
|---|---|
| 0.00-0.20 | Should usually ask, probe, audit, or abstain. |
| 0.20-0.40 | Hypothesis or heavily caveated answer. |
| 0.40-0.60 | Useful but uncertain answer. |
| 0.60-0.80 | Direct with caveats unless risk is high. |
| 0.80-1.00 | Direct only when evidence and calibration support it. |

## Probing As Training

Probing sessions should deliberately exercise strong domains, weak domains, recently corrected patterns, high-use project areas, known wrong-scope boundaries, source-poor regions, stale procedures, professor-reviewed topics, and high-value project decisions.

The purpose is to train both memory content and self-confidence calibration.

## Calibration Changes Are Versioned

Calibration updates should not silently change behavior. They should produce reviewable profile updates:

```text
Calibration events
  -> aggregate health metrics
  -> proposed profile threshold/shape update
  -> review or policy approval where needed
  -> new profile version
  -> old traces remain interpreted by old profile
```

## Overconfidence Handling

If the system is repeatedly overconfident in a pattern, lower allowed posture, require additional source anchors, add known failure pattern, create regression tests, schedule probing drills, and trigger professor challenge for high-impact cases.

## Underconfidence Handling

If the system is repeatedly underconfident in a pattern, add reinforcement evidence and allow stronger posture for scoped low-risk cases while keeping source requirements intact.

## Quality Rule

A single correction should not permanently define incompetence. A single success should not permanently define competence. Use evidence accumulation, recency windows, risk weighting, and review policy.
