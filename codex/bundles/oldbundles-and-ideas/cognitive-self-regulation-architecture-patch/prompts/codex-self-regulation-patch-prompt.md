# Copy/Paste Prompt For Codex

You are working in the `cognitive-memory-architecture-v2` architecture bundle for CanDoItAll. The architecture is still being improved before implementation begins. Do not implement runtime application code yet. Update the architecture bundle only.

## Mission

Improve the current Cognitive Memory architecture with an explicit **Cognitive Self-Regulation** layer.

The current bundle already has Cognitive Workspace, Attention Router, Claim/Evidence/Belief Ledger, Prediction Error and Salience Signal Ledger, Score Geometry, Interactive Probing, Confidence Calibration, Procedural Memory, Replay, and Metamemory Answer Gate. These are necessary but distributed. The missing architecture element is an explicit self-model and shared self-regulation orchestrator that connects them into a stable, auditable, project-aware control loop.

Do not model this as consciousness, emotion, personality, or anthropomorphic ego. Model it as:

```text
calibrated agency under epistemic uncertainty
```

The system must be able to act decisively when evidence, context fit, calibration, and risk permit it; express uncertainty when evidence is incomplete; and abstain, clarify, probe, review, source-audit, or escalate when required.

## Add New Architecture Files

Add new files using numbering that follows the current bundle. Suggested names:

```text
architecture/27-cognitive-self-regulation-layer.md
architecture/28-self-model-and-epistemic-identity.md
architecture/29-calibration-health-and-probing-training.md
architecture/30-professor-review-and-escalation.md
```

## Add New Contracts And Diagrams

Add:

```text
contracts/csharp/CognitiveMemory.SelfRegulationContracts.cs
diagrams/19-cognitive-self-regulation-overview.mmd
diagrams/20-self-regulation-answer-sequence.mmd
diagrams/21-calibration-training-loop.mmd
diagrams/22-professor-review-flow.mmd
```

Use the contract sketches and diagrams in this patch bundle as source material.

## Update Existing Architecture Files

Update these existing v2 files:

```text
architecture/17-neuro-cognitive-integration-layer.md
architecture/18-cognitive-workspace-and-attention-router.md
architecture/19-prediction-error-salience-signal-ledger.md
architecture/20-claim-evidence-belief-ledger.md
architecture/24-metamemory-confidence-and-abstention.md
architecture/26-score-geometry-driver.md
```

Required changes:

- Add Cognitive Self-Regulation as an explicit control layer.
- Add `SelfRegulationAssessmentId` and `AnswerPostureDecisionId` references where relevant.
- Make Attention Router consume Self-Regulation Assessment.
- Make Metamemory Answer Gate consume Self-Regulation Assessment and Answer Posture.
- State that Answer Gate can be stricter than Self-Regulation, but not looser without a new score trace.
- Add signal kinds for overconfidence pressure, underconfidence pressure, known failure pattern matched, professor review required, professor review disagreement, self-model updated, calibration drift, humility trigger fired, and confidence reinforced.
- Clarify that Self-Regulation can submit mutation candidates but cannot mutate canonical truth directly.
- Add Professor Review and Self-Model evidence as governed evidence only, not direct truth.
- Add Self-Regulation score spaces and dimensions.
- Add tests rejecting scalar-only self-regulation behavior.

## Update Existing Contracts

Update:

```text
contracts/csharp/CognitiveMemory.NeuroPatchContracts.cs
contracts/csharp/CognitiveMemory.ScoringContracts.cs
contracts/csharp/InteractiveMemoryProbingContracts.cs
```

Required changes:

- Add links from attention decisions and answer gate decisions to self-regulation assessment/posture ids.
- Add self-regulation assessment input to answer gate request or equivalent request metadata.
- Add self-regulation score spaces/dimensions/evidence kinds using `contracts/csharp/ScoreGeometrySelfRegulationPatch.md` as guidance.
- Add self-regulation assessment id in probe answer metadata.
- Add predicted posture and actual outcome fields to calibration records.
- Run normal contract consistency checks before extending enum values and cross-file references.

## Add Requirements

Add new requirements after current neuro/metamemory requirements. Use the next available IDs if these are already taken:

- FR-055 Cognitive Self-Model
- FR-056 Self-Regulation Assessment
- FR-057 Humility Trigger Engine
- FR-058 Answer Posture Selection
- FR-059 Calibration Health Aggregates
- FR-060 Professor Review Escalation
- FR-061 Post-Outcome Self-Regulation Feedback
- NFR-034 Self-Regulation Auditability
- NFR-035 Non-Anthropomorphic Self-Regulation Safety
- NFR-036 Calibration Profile Versioning
- NFR-037 Professor Review Governance
- NFR-038 No Scalar-Only Self-Regulation

## Add Subbundles

Add these subbundles after current `20-architecture-integration-closure` or at the next available positions:

```text
subbundles/21-cognitive-self-model
subbundles/22-self-regulation-orchestrator
subbundles/23-calibration-health-and-probing-training
subbundles/24-professor-review-escalation
subbundles/25-self-regulation-ui
subbundles/26-architecture-integration-closure
```

Also update existing `19-metamemory-abstention-calibration` so it depends on or is reopened after `22-self-regulation-orchestrator`.

## Required Concepts

### Cognitive Self-Model

A durable, scoped, evidence-backed record of operating principles, allowed/restricted task categories, domain competence profiles, weak domains, known failure patterns, and default self-regulation policy. It is not a prompt persona.

### Self-Regulation Assessment

A traceable assessment containing self-model id, domain competence, known failure pattern matches, calibration health, current state, humility triggers, confidence reinforcements, warnings, required operations, and score trace.

### Humility Trigger Engine

Detects when to reduce confidence, caveat, clarify, source-audit, probe, review, professor-review, or abstain. Include source-poor high-risk answer, contradiction pressure, wrong-scope pattern, generated-summary primary support, stale volatile source, redaction-limited proof, and cognitive load saturation.

### Confidence Reinforcement

Allows stronger posture only through evidence: confirmed probes, passing regressions, human review, workflow validation, multiple independent sources, stable project decision records, and long observation windows without contradiction.

### Answer Posture Decision

Supports direct confident, direct with caveats, preliminary reaction, hypothesis, clarification question, source audit request, probe question, review required, professor review required, and abstain.

### Calibration Health

Aggregates calibration evidence by domain, task type, model profile, risk category, and feature pattern. Include expected calibration error or equivalent, Brier/squared calibration loss, signed confidence bias, overconfidence rate, underconfidence rate, abstention quality, wrong-scope rate, and source-insufficient rate.

### Professor Review

Large LLM/human expert review is a challenge/audit/escalation mechanism, not source truth. It can propose probes, source audits, review items, regression candidates, and mutation candidates through governance. It cannot directly create canonical truth.

### Post-Outcome Feedback

Convert answer/probe/workflow/review outcomes into calibration records, prediction errors, salience signals, regression candidates, probing drills, known failure pattern updates, review items, and self-model update proposals.

## Do Not Do

- Do not implement runtime code in this architecture patch.
- Do not call the system conscious.
- Do not replace claim/evidence/belief ledger, probing, score geometry, replay, or answer gate.
- Do not use prompt persona as self-model.
- Do not allow self-model, professor review, generated summary, salience, prediction error, or probing feedback to directly become canonical truth.
- Do not use scalar confidence as the behavior-affecting decision model.
- Do not tune final thresholds without calibration data.

## Final Output Expected From Codex

Codex should output an updated architecture bundle with changed files, new files, updated manifest/checksums, updated traceability, updated validation plan, and a short self-review proving that source truth, projection-only Qdrant, probing safety, review governance, mutation authority, score geometry, and answer-gating boundaries remain intact.
