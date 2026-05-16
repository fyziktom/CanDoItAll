# Cognitive Self-Regulation Requirements

## Functional Requirements

### FR-055: Cognitive Self-Model

The system shall define a durable, scoped, evidence-backed self-model containing operating principles, allowed task categories, restricted task categories, domain competence profiles, weak domains, known failure patterns, and default self-regulation policy.

### FR-056: Self-Regulation Assessment

The system shall evaluate important answers, tool actions, workflow actions, probes, reviews, and memory mutation requests through a self-regulation assessment that includes workspace state, self-model, competence profiles, calibration health, known failure pattern matches, score trace, warnings, and required operations.

### FR-057: Humility Trigger Engine

The system shall define a reusable humility trigger engine that detects conditions requiring reduced confidence, caveats, clarification, source audit, probing, review, professor review, or abstention.

### FR-058: Answer Posture Selection

The system shall select an explicit answer posture before answer rendering. Supported postures include direct confident, direct with caveats, preliminary reaction, hypothesis, clarification question, source audit request, probe question, review required, professor review required, and abstain.

### FR-059: Calibration Health Aggregates

The system shall aggregate calibration evidence by domain, task type, model profile, risk category, and feature pattern. Required metrics include expected calibration error or equivalent, Brier score or squared calibration loss, signed confidence bias, overconfidence rate, underconfidence rate, abstention quality, wrong-scope rate, and source-insufficient rate.

### FR-060: Professor Review Escalation

The system shall support escalation to a professor review service for challenge, contradiction hunt, architecture review, calibration review, source sufficiency review, alternative hypothesis review, failure mode review, and learning expansion.

### FR-061: Post-Outcome Self-Regulation Feedback

The system shall convert answer, probe, workflow, review, and professor-review outcomes into calibration records, prediction errors, salience signals, regression candidates, probing drills, failure pattern updates, review items, replay jobs, and self-model update proposals.

## Non-Functional Requirements

### NFR-037: Self-Regulation Auditability

Every behavior-affecting self-regulation decision shall preserve evidence refs, score evaluation trace, algorithm/profile version, actor/model profile, and timestamp.

### NFR-038: Non-Anthropomorphic Self-Regulation Safety

The architecture shall not describe Self-Regulation as consciousness, emotional simulation, or autonomous ego. It shall describe it as calibrated agency and epistemic control.

### NFR-039: Calibration Profile Versioning

Calibration and self-model profile changes shall be versioned. Old traces must not be reinterpreted by new profiles without migration or recalculation.

### NFR-040: Professor Review Governance

Professor review shall not bypass source truth, access policy, redaction, mutation authority, human review, or safety policy.

### NFR-041: No Scalar-Only Self-Regulation

Self-regulation assessment, answer posture selection, professor-review routing, and calibration health shall use score geometry traces. Scalar display confidence is allowed only as a rendering/projection aid.

## Acceptance Criteria

- Cognitive self-model is structured data, not prompt persona.
- Domain competence is scoped by project, domain, task, model profile, and role.
- Known failure patterns preserve trigger conditions, score shapes, mitigation, and regression/probe links.
- Self-model updates require evidence and profile versioning.
- Self-regulation assessments classify calibrated, exploratory, overconfident, underconfident, defensive, fragmented, source-poor, high-risk-unverified, and professor-review-needed states.
- Attention Router consumes self-regulation assessment and posture.
- Metamemory Answer Gate consumes self-regulation assessment and posture, and cannot become looser without a new score trace.
- Source-poor high-risk answers are downgraded, audited, reviewed, or abstained.
- Generated-summary primary support cannot produce a stable confident answer.
- Repeated probe confirmation can reinforce confidence through reviewable evidence.
- Single praise/success does not permanently increase truth confidence.
- Professor review suggestions become probes, source audits, review items, regressions, learning proposals, or mutation candidates only through governance.
- Score Geometry includes self-regulation score spaces, dimensions, and scalar-only rejection tests.
