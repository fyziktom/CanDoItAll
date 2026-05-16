# Normalized Requirements: Cognitive Self-Regulation Patch

## FR-055: Cognitive Self-Model

The system shall define a durable, scoped, evidence-backed self-model containing operating principles, allowed/restricted task categories, domain competence profiles, weak domains, known failure patterns, and default self-regulation policy.

## FR-056: Self-Regulation Assessment

The system shall evaluate requests through a Self-Regulation Assessment before important answers, tool actions, workflow actions, probes, reviews, and memory mutations.

## FR-057: Humility Trigger Engine

The system shall define a reusable humility trigger engine that detects conditions requiring reduced confidence, caveats, clarification, source audit, probing, review, professor review, or abstention.

## FR-058: Answer Posture Selection

The system shall select an explicit answer posture before answer rendering. Supported postures include direct confident, direct with caveats, preliminary reaction, hypothesis, clarification question, source audit request, probe question, review required, professor review required, and abstain.

## FR-059: Calibration Health Aggregates

The system shall aggregate calibration evidence by domain, task type, model profile, risk category, and feature pattern. Required metrics include expected calibration error, Brier score or squared calibration loss, signed confidence bias, overconfidence rate, underconfidence rate, abstention quality, wrong-scope rate, and source-insufficient rate.

## FR-060: Professor Review Escalation

The system shall support escalation to a large-model professor review service for challenge, contradiction hunt, architecture review, calibration review, source sufficiency review, alternative hypothesis review, failure mode review, and learning expansion.

## FR-061: Post-Outcome Self-Regulation Feedback

The system shall convert answer/probe/workflow/review outcomes into calibration records, prediction errors, salience signals, regression candidates, probing drills, failure pattern updates, review items, and self-model update proposals.

## NFR-034: Self-Regulation Auditability

Every behavior-affecting self-regulation decision shall preserve evidence refs, score evaluation trace, algorithm/profile version, actor/model profile, and timestamp.

## NFR-035: Non-Anthropomorphic Safety

The architecture shall not describe Self-Regulation as consciousness, emotional simulation, or autonomous ego. It shall describe it as calibrated agency and epistemic control.

## NFR-036: Calibration Profile Versioning

Calibration and self-model profile changes shall be versioned. Old traces must not be reinterpreted by new profiles without migration or recalculation.

## NFR-037: Professor Review Governance

Professor Review shall not bypass source truth, access policy, redaction, mutation authority, human review, or safety policy.

## NFR-038: No Scalar-Only Self-Regulation

Self-Regulation assessment, answer posture selection, professor-review routing, and calibration health must use score geometry traces. Scalar display confidence is allowed only as a rendering/projection aid.
