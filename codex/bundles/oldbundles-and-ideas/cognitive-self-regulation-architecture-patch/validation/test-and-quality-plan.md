# Test And Quality Plan: Cognitive Self-Regulation

## Contract Tests

- `CognitiveSelfModelRecord` can be scoped by project, domain/task, model profile, and role.
- `DomainCompetenceProfileRecord` references score geometry and calibration aggregate evidence.
- `KnownFailurePatternRecord` contains trigger kinds, score shape, mitigation, and regression/probe links.
- `SelfRegulationAssessment` references self-model, score trace, triggers, reinforcements, warnings, and required operations.
- `AnswerPostureDecision` references Self-Regulation Assessment and score trace.
- `ProfessorReviewResult` includes model profile, prompt profile version, output hash, review trace, and governance flags.

## Unit Tests

### Self-Model

- Strong domain returns higher competence fit only when evidence supports it.
- Weak domain triggers lower autonomy or professor review.
- Known failure pattern match produces humility trigger.
- Self-model update without evidence is rejected.

### Humility Trigger Engine

- Source-poor high-risk answer triggers source audit/review/abstain.
- Wrong-scope known failure pattern triggers clarification and candidate inhibition.
- Generated summary as primary support prevents direct confident answer.
- Redaction-limited source triggers warning or abstention.
- Stale volatile topic triggers source audit.

### Answer Posture

- Sufficient evidence + good calibration + low risk -> direct confident.
- Medium evidence + low risk -> direct with caveats.
- Weak evidence + exploratory mode -> hypothesis.
- Ambiguous scope -> clarification question.
- Missing source anchors -> source audit request.
- High-risk novelty -> professor review or human review.
- Unsafe/misleading answer -> abstain.

### Calibration Health

- High confidence + wrong answer increments overconfidence rate.
- Low confidence + correct answer increments underconfidence rate.
- Binned calibration computes signed bias.
- Profile version changes do not reinterpret old traces.
- Single event does not silently retune policy.

### Professor Review

- Escalation is triggered by high-impact novelty.
- Professor output cannot directly mutate canonical truth.
- Professor suggestions create probe/source-audit/review/regression candidates.
- Access/redaction policy is preserved.

## Negative Tests

- Self-model cannot mark unsupported claim as true.
- Professor Review cannot bypass mutation authority.
- Salience/reward cannot increase belief without evidence.
- Display confidence cannot drive answer posture alone.
- Scalar-only self-regulation implementation is rejected.
- Prompt persona cannot replace self-model records.
- User praise cannot create competence evidence.
