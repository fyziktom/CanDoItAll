# Acceptance Criteria: Cognitive Self-Regulation Patch

## Self-Model

- [ ] Cognitive self-model is defined as structured data, not prompt persona.
- [ ] Domain competence is scoped by project/domain/task/model profile.
- [ ] Known failure patterns have trigger conditions, score shapes, mitigation, and regression/probe links.
- [ ] Self-model updates require evidence and profile versioning.
- [ ] Self-model cannot mutate canonical truth or bypass policy.

## Self-Regulation Assessment

- [ ] Assessment includes self-model id, competence profiles, known failure pattern matches, score trace, state, warnings, required operations, and evidence refs.
- [ ] Assessment can classify calibrated, exploratory, overconfident, underconfident, defensive, fragmented, source-poor, high-risk-unverified, and professor-review-needed states.
- [ ] Attention Router consumes assessment.
- [ ] Metamemory Answer Gate consumes assessment and posture.

## Humility Triggers

- [ ] Source-poor high-risk answers are downgraded, audited, reviewed, or abstained.
- [ ] Wrong-scope failure patterns trigger clarification/inhibition.
- [ ] Generated-summary primary support cannot produce a stable confident answer.
- [ ] Stale volatile sources trigger source audit.
- [ ] Redaction-limited proof triggers warning or abstention.
- [ ] Trigger records preserve score traces and evidence refs.

## Confidence Reinforcement

- [ ] Repeated probe confirmation can reinforce confidence through reviewable evidence.
- [ ] Regression pass and workflow validation can reinforce confidence for scoped feature patterns.
- [ ] Reinforcement does not erase source requirements or contradiction dimensions.
- [ ] Single praise/success does not permanently increase truth confidence.

## Professor Review

- [ ] Professor Review supports challenge, contradiction hunt, architecture review, calibration review, source sufficiency review, alternative hypotheses, failure mode review, and learning expansion.
- [ ] Professor Review records model profile, prompt/profile version, input context ids, output hash, and review trace.
- [ ] Professor Review suggestions become probes/reviews/source audits/regressions/claim operation candidates only through governance.
- [ ] Professor Review cannot directly create truth.

## Integration

- [ ] `architecture/17`, `18`, `19`, `20`, `24`, and `26` are updated.
- [ ] C# contracts include Self-Regulation records and services.
- [ ] Score Geometry includes self-regulation-related score spaces/dimensions/evidence kinds.
- [ ] Probing metadata can reference self-regulation assessment/posture.
- [ ] Validation plan includes negative tests for policy bypass and scalar-only decisions.
