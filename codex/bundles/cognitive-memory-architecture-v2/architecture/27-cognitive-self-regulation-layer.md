# 27 Cognitive Self-Regulation Layer

## Purpose

Add a Cognitive Self-Regulation layer that coordinates self-model, calibration health, humility triggers, answer posture, professor review, probing, and post-outcome recovery.

This layer turns distributed uncertainty signals into disciplined behavior. It is not consciousness, emotion, personality, or an autonomous identity. It is calibrated agency under epistemic uncertainty.

## Placement

```text
User / Agent / Workflow request
  -> Cognitive Workspace
  -> Self-Regulation Orchestrator
       -> Self-Model Store
       -> Calibration Health Service
       -> Humility Trigger Engine
       -> Score Geometry Driver
       -> Professor Review Service when required
  -> Attention Router
  -> Recall / Source Audit / Probe / Review / Learning / Replay
  -> Metamemory Answer Gate
  -> Answer Renderer / Tool Executor
  -> Outcome Observation
  -> Calibration / Prediction Error / Salience / Review / Replay / Probing
```

## Responsibilities

| Responsibility | Meaning |
|---|---|
| Self-model awareness | Load scoped operating principles, allowed/restricted task categories, competence profiles, weak domains, and known failure patterns. |
| Calibration health | Compare predicted confidence/posture with outcomes by domain, task type, model profile, risk category, and feature pattern. |
| Humility triggers | Detect source-poor, contradicted, stale, redacted, high-risk, overconfident, wrong-scope, or cognitive-load-saturated conditions. |
| Confidence reinforcement | Allow stronger posture only from repeated, reviewable evidence. |
| Answer posture selection | Choose direct, caveated, preliminary, hypothesis, clarification, source-audit, probe, review, professor-review, or abstention posture. |
| Escalation | Route high-impact novelty, weak competence, contradiction pressure, or poor calibration to human/professor/source-audit review. |
| Recovery | Convert failures and confirmations into calibration records, known failure pattern proposals, probes, regressions, replay jobs, signals, and review items. |

## Core Records

- `SelfRegulationAssessment`
- `AnswerPostureDecision`
- `HumilityTriggerRecord`
- `ConfidenceReinforcementRecord`
- `SelfRegulationOutcomeRecord`
- `ProfessorReviewRequest`
- `ProfessorReviewResult`

Every behavior-affecting record must preserve:

- project and workspace scope,
- score evaluation trace,
- evidence refs,
- actor or model profile,
- policy/profile version,
- timestamp,
- required next actions or warnings.

## Relationship To Existing Components

### Cognitive Workspace

Self-Regulation reads the active workspace frame and may add open questions, warnings, posture constraints, or required operations. Workspace content remains temporary control state, not source truth.

### Attention Router

Attention routing consumes `SelfRegulationAssessment` and `AnswerPostureDecision`. For example, if self-regulation requires source audit or clarification, attention routing must not choose answer-from-workspace without a new trace explaining why the constraint changed.

### Metamemory Answer Gate

The answer gate remains the final answer-time boundary. It consumes assessment and posture. It can become stricter when final synthesis introduces unsupported claims, contradiction, source insufficiency, redaction, or new risk. It cannot become looser than self-regulation without a new score-geometry evaluation trace.

### Claim/Evidence/Belief Ledger

Self-Regulation reads belief states, evidence anchors, support/attack links, and context frames. It may submit claim mutation candidates through `IMemoryMutationAuthority`; it must not mutate claims directly.

### Prediction Error And Salience Signal Ledger

Self-Regulation publishes prediction errors and cognitive signals such as overconfidence pressure, underconfidence pressure, known failure pattern matched, professor review required, professor review disagreement, self-model updated, calibration drift, humility trigger fired, and confidence reinforced.

### Interactive Probing

Probing is the training loop for both memory content and calibration. Probe answers should record self-regulation assessment id, predicted posture, actual outcome, and calibration evidence.

### Score Geometry

Self-Regulation assessment, answer posture selection, professor-review routing, and calibration health use declared score spaces. Display confidence is allowed only as derived rendering data.

## Healthy Regulation States

| State | Meaning | Desired behavior |
|---|---|---|
| `Calibrated` | Confidence matches evidence, context, risk, and historical correctness. | Answer or act according to policy. |
| `Exploratory` | Topic is plausible but incompletely supported. | Label hypotheses, ask probes, seek sources. |
| `Overconfident` | Confidence exceeds evidence or historical correctness. | Downgrade posture, require proof, review, or challenge. |
| `Underconfident` | Evidence is sufficient but the system is overly hesitant. | Reinforce confidence through reviewable calibration update. |
| `Defensive` | Old claim is protected despite attacks. | Force claim review and contradiction analysis. |
| `Fragmented` | Scope/context is mixed or unclear. | Clarify and inhibit related-wrong candidates. |
| `SourcePoor` | Plausible answer lacks enough source anchors. | Audit, caveat, probe, or abstain by risk. |
| `HighRiskUnverified` | Consequence risk is high and validation is weak. | Require review, validation, or abstention. |
| `ProfessorReviewNeeded` | Local competence/evidence is insufficient for high-impact synthesis. | Escalate to governed challenge/audit review. |

## Non-Goals

- Do not create a black-box consciousness layer.
- Do not introduce persona drift or emotional simulation.
- Do not let self-confidence override evidence.
- Do not let user praise increase truth confidence without evidence.
- Do not let professor review bypass source truth, access policy, review policy, or mutation authority.
