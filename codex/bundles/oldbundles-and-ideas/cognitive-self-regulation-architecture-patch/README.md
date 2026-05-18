# Cognitive Self-Regulation Architecture Patch Bundle

## Mission

Improve the current `cognitive-memory-architecture-v2` bundle with an explicit **Cognitive Self-Regulation** layer.

The current bundle already contains important pieces: Cognitive Workspace, Attention Router, Claim/Evidence/Belief Ledger, Prediction Error and Salience Signal Ledger, Score Geometry, Interactive Probing, Confidence Calibration, and Metamemory Answer Gate. These are necessary but currently distributed. The missing architecture element is an explicit self-model and a shared self-regulation orchestrator that connects them into a stable, auditable, project-aware control loop.

This patch is **architecture-only**. Do not implement runtime code yet. Codex should use this bundle to update the main architecture bundle before implementation begins.

## Core Decision

Do not model “ego” as personality, consciousness, emotion, or anthropomorphic identity. Model it as:

```text
calibrated agency under epistemic uncertainty
```

The Self-Regulation layer must make the system able to:

- act decisively when evidence, context fit, calibration, and risk permit it,
- express uncertainty clearly when evidence is incomplete,
- abstain, clarify, probe, review, source-audit, or escalate when required,
- remember its own historical failure patterns,
- detect overconfidence and underconfidence,
- prevent generated summaries or fluent answers from becoming source truth,
- use larger LLMs as professors/challengers/auditors, not as unquestioned authorities.

## How Codex Should Use This Bundle

Patch the existing `cognitive-memory-architecture-v2` bundle. Add new architecture files, contracts, diagrams, subbundles, requirements, acceptance criteria, validation plans, and traceability rows. Also update existing files where the new layer must be consumed:

- `architecture/17-neuro-cognitive-integration-layer.md`
- `architecture/18-cognitive-workspace-and-attention-router.md`
- `architecture/19-prediction-error-salience-signal-ledger.md`
- `architecture/20-claim-evidence-belief-ledger.md`
- `architecture/24-metamemory-confidence-and-abstention.md`
- `architecture/26-score-geometry-driver.md`
- `contracts/csharp/CognitiveMemory.NeuroPatchContracts.cs`
- `contracts/csharp/CognitiveMemory.ScoringContracts.cs`
- `contracts/csharp/InteractiveMemoryProbingContracts.cs`
- `requirements/01-normalized-requirements.md`
- `requirements/02-acceptance-criteria.md`
- `validation/test-and-quality-plan.md`
- `traceability/01-requirement-traceability.md`
- `subbundles/19-metamemory-abstention-calibration/README.md`

Add new files using numbering that follows the current bundle, for example:

- `architecture/27-cognitive-self-regulation-layer.md`
- `architecture/28-self-model-and-epistemic-identity.md`
- `architecture/29-calibration-health-and-probing-training.md`
- `architecture/30-professor-review-and-escalation.md`
- `contracts/csharp/CognitiveMemory.SelfRegulationContracts.cs`
- `subbundles/21-cognitive-self-model/README.md`
- `subbundles/22-self-regulation-orchestrator/README.md`
- `subbundles/23-calibration-health-and-probing-training/README.md`
- `subbundles/24-professor-review-escalation/README.md`
- `subbundles/25-self-regulation-ui/README.md`
- `subbundles/26-architecture-integration-closure/README.md`

## Non-Negotiable Constraints

- Self-Regulation must not become a black-box autonomous consciousness layer.
- Self-Regulation must not mutate canonical memory directly.
- Self-Regulation must not override source policy, access policy, redaction, or mutation authority.
- Professor Review output must be evidence/challenge/review input, not source truth.
- Display confidence must never be the decision model.
- Calibration updates must be versioned and reviewable; do not silently retune thresholds from single events.
- Generated summaries, hypotheses, and LLM opinions remain clearly labeled and must not become stable beliefs without evidence and review.

## Bundle Layout

- `analysis/` current architecture audit and gap analysis.
- `architecture/` target design for Self-Regulation, Self-Model, calibration health, and professor review.
- `contracts/csharp/` C# contract sketch and score geometry patch notes.
- `diagrams/` Mermaid diagrams for overview, sequence, calibration loop, and professor review.
- `requirements/` normalized functional and non-functional requirements.
- `plan/` execution order and architecture patch plan.
- `subbundles/` execution-grade architecture workstreams for Codex.
- `prompts/` copy-paste prompts for Codex.
- `validation/` test matrix and quality plan.
- `traceability/` requirement-to-subbundle mapping.
- `reviews/` self-review of this patch bundle.
