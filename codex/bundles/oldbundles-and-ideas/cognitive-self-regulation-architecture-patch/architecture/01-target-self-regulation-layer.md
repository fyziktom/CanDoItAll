# Target Architecture: Cognitive Self-Regulation Layer

## Purpose

Add a Cognitive Self-Regulation layer that coordinates self-model, confidence calibration, humility triggers, answer posture, escalation, probing, and post-outcome learning.

This layer turns distributed uncertainty signals into disciplined behavior.

## Definition

Cognitive Self-Regulation is not consciousness. It is the system's ability to regulate its own actions under uncertainty using evidence, context, historical performance, risk, and policy.

```text
Cognitive Self-Regulation = calibrated agency under epistemic uncertainty
```

## Placement

```text
User / Agent / Workflow request
  -> Cognitive Workspace
  -> Self-Regulation Orchestrator
       -> Self-Model Store
       -> Score Geometry Driver
       -> Humility Trigger Engine
       -> Calibration Health Service
       -> Escalation Policy Engine
       -> Professor Review Service when needed
  -> Attention Router
  -> Recall / Source Audit / Probe / Review / Learning / Replay
  -> Metamemory Answer Gate
  -> Answer Renderer / Tool Executor
  -> Outcome Observation
  -> Calibration Ledger / Prediction Error / Salience / Replay / Probing
```

## Main Responsibilities

| Responsibility | Meaning |
|---|---|
| Self-model awareness | Know the agent/project/domain role, principles, competence, limits, and failure patterns. |
| Confidence calibration | Compare predicted confidence with actual correctness and update profile evidence. |
| Humility triggers | Detect when fluent answers should be downgraded, caveated, probed, reviewed, escalated, or blocked. |
| Confidence reinforcement | Detect when evidence supports stronger confidence without becoming overconfident. |
| Answer posture selection | Choose direct, caveated, hypothesis, clarification, source audit, probe, professor review, or abstention mode. |
| Escalation | Decide when a larger LLM, human review, source audit, or regression test is required. |
| Recovery | Convert mistakes into correction candidates, failure patterns, calibration updates, probes, regression tests, and replay jobs. |

## Relationship To Existing Components

### Cognitive Workspace

Self-Regulation reads the current workspace to understand active goals, focus slots, inhibited candidates, open questions, and cognitive load.

It may add open questions, required actions, humility warnings, or professor-review requirements to the workspace, but it must not turn workspace content into source truth.

### Attention Router

Attention routing should consume Self-Regulation assessment. For example, an attention decision should know when self-regulation requires clarification, source audit, probing, replay, review, or abstention.

### Metamemory Answer Gate

Metamemory Answer Gate remains the final answer-time boundary. It should consume `SelfRegulationAssessment` and `AnswerPostureDecision` instead of independently recomputing all self-regulation logic.

### Claim/Evidence/Belief Ledger

Self-Regulation reads claim belief states and evidence anchors. It may submit mutation candidates through `IMemoryMutationAuthority`, but it must not mutate claims directly.

### Prediction Error And Salience Signal Ledger

Self-Regulation publishes prediction errors and cognitive signals such as overconfidence, underconfidence, failure pattern matched, professor review required, self-model updated, and calibration drift.

### Interactive Probing

Probing is the training loop for Self-Regulation. It tests not only factual memory but also calibration: when the system thought it knew, when it doubted, and whether that doubt was justified.

### Score Geometry

Self-Regulation must use typed score spaces and evaluation traces. It must not use a single hidden confidence scalar.

## Core Flow

```text
1. Request enters a workspace frame.
2. Self-Regulation loads relevant self-model profiles and calibration history.
3. It evaluates current epistemic state via score geometry.
4. Humility triggers and reinforcement rules are applied.
5. It produces a SelfRegulationAssessment.
6. It selects an AnswerPostureDecision or RequiredOperation.
7. Attention Router and Metamemory Gate consume the assessment.
8. Output/tool action is rendered or blocked.
9. Outcome feedback updates calibration, failure patterns, probes, replay, and review queues.
```

## Healthy Ego States

| State | Meaning | Desired Behavior |
|---|---|---|
| `Calibrated` | Confidence matches evidence and historical correctness. | Answer or act according to risk policy. |
| `Exploratory` | Topic is promising but evidence is incomplete. | Label hypotheses, ask probes, seek sources. |
| `Overconfident` | Confidence exceeds evidence or historical correctness. | Downgrade posture, require proof, review, or professor challenge. |
| `Underconfident` | Evidence is sufficient but system is unnecessarily hesitant. | Reinforce confidence through reviewable calibration update. |
| `Defensive` | System protects an old claim despite attacks. | Force claim review and contradiction analysis. |
| `Fragmented` | Context is unclear or mixed across scopes. | Clarify, inhibit related-wrong candidates, avoid synthesis. |
| `SourcePoor` | Answer may be plausible but evidence anchors are insufficient. | Source audit, probe, caveat, or abstain depending on risk. |
| `HighRiskUnverified` | Consequences are high and validation is weak. | Require review, validation, or abstention. |

## Non-Goals

- Do not create an autonomous consciousness model.
- Do not introduce persona drift or emotional simulation.
- Do not let self-confidence override evidence.
- Do not let user praise increase truth confidence without evidence.
- Do not let professor review bypass source truth, access policy, or mutation authority.
