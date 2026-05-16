# Integration With Existing Cognitive Memory V2 Bundle

## Add New Architecture Files

Codex should add these files to the main bundle:

- `architecture/27-cognitive-self-regulation-layer.md`
- `architecture/28-self-model-and-epistemic-identity.md`
- `architecture/29-calibration-health-and-probing-training.md`
- `architecture/30-professor-review-and-escalation.md`

## Update Existing Architecture Files

### `architecture/17-neuro-cognitive-integration-layer.md`

Add Cognitive Self-Regulation as an explicit control layer between workspace/attention/metamemory/calibration/professor review.

### `architecture/18-cognitive-workspace-and-attention-router.md`

Add `SelfRegulationAssessmentId` and `AnswerPostureDecisionId` references to attention decisions where applicable. Attention routing should consume self-regulation assessment.

### `architecture/19-prediction-error-salience-signal-ledger.md`

Add signal kinds for overconfidence pressure, underconfidence pressure, known failure pattern matched, professor review required, professor review disagreement, self-model updated, calibration drift, humility trigger fired, and confidence reinforced.

### `architecture/20-claim-evidence-belief-ledger.md`

Clarify that self-regulation can submit claim mutation candidates but cannot mutate truth directly. Add professor review and self-model evidence as possible evidence directions only after review policy.

### `architecture/24-metamemory-confidence-and-abstention.md`

Update the answer gate so it consumes Self-Regulation Assessment and Answer Posture. The gate can be stricter than the assessment but not looser without a new score trace.

### `architecture/26-score-geometry-driver.md`

Add Self-Regulation score spaces and dimensions. Add tests rejecting scalar-only self-regulation behavior.

## Update Contracts

Add `contracts/csharp/CognitiveMemory.SelfRegulationContracts.cs`.

Update `CognitiveMemory.ScoringContracts.cs` with new score spaces and dimensions. Suggested additions are listed in `contracts/csharp/ScoreGeometrySelfRegulationPatch.md`.

Update `CognitiveMemory.NeuroPatchContracts.cs`:

- add links from attention decisions and answer gate decisions to self-regulation assessment/posture ids,
- consume `SelfRegulationAssessment` inside answer gate request,
- run a normal contract consistency audit before extending enums and cross-file references.

Update `InteractiveMemoryProbingContracts.cs`:

- include self-regulation assessment id in probe answer metadata,
- include predicted posture and actual outcome in calibration records.

## Update Requirements

Add new requirements after the current neuro/metamemory requirements. Suggested IDs:

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

## Update Subbundles

Add new subbundles after current `20-architecture-integration-closure`:

- `21-cognitive-self-model`
- `22-self-regulation-orchestrator`
- `23-calibration-health-and-probing-training`
- `24-professor-review-escalation`
- `25-self-regulation-ui`
- `26-architecture-integration-closure`

Update current `19-metamemory-abstention-calibration` to depend on `22-self-regulation-orchestrator` or to be reopened after it.
