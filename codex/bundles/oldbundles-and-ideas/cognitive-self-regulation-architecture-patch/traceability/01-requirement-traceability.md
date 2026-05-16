# Requirement Traceability: Cognitive Self-Regulation Patch

| Requirement | Primary Subbundle | Validation Evidence |
|---|---|---|
| FR-055 Cognitive Self-Model | `21-cognitive-self-model` | self-model scoping, competence, failure pattern, evidence-backed update tests |
| FR-056 Self-Regulation Assessment | `22-self-regulation-orchestrator` | assessment/posture trace tests and attention/answer-gate integration tests |
| FR-057 Humility Trigger Engine | `22-self-regulation-orchestrator` | trigger tests for source-poor, wrong-scope, generated-summary, redaction, stale-source cases |
| FR-058 Answer Posture Selection | `22-self-regulation-orchestrator` + `25-self-regulation-ui` | posture selection tests and UI trace visibility |
| FR-059 Calibration Health Aggregates | `23-calibration-health-and-probing-training` | calibration bins, ECE/Brier/bias, over/underconfidence tests |
| FR-060 Professor Review Escalation | `24-professor-review-escalation` | escalation trigger and governance tests |
| FR-061 Post-Outcome Feedback | `23-calibration-health-and-probing-training` | outcome-to-calibration/prediction-error/salience/review/replay tests |
| NFR-034 Auditability | all subbundles | score trace and evidence ref tests |
| NFR-035 Non-Anthropomorphic Safety | `26-architecture-integration-closure` | architecture review checklist |
| NFR-036 Calibration Profile Versioning | `23-calibration-health-and-probing-training` | old trace profile version tests |
| NFR-037 Professor Review Governance | `24-professor-review-escalation` | no direct truth mutation tests |
| NFR-038 No Scalar-Only Self-Regulation | `22-self-regulation-orchestrator` | scalar-only rejection tests |
