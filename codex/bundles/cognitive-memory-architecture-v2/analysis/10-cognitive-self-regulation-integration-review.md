# Cognitive Self-Regulation Integration Review

## Patch Finding

The self-regulation patch correctly identifies that the current v2 bundle has most of the low-level ingredients but lacks a named coordination layer:

- Cognitive Workspace and Attention Router hold focus and choose operations.
- Claim/Evidence/Belief Ledger models truth, uncertainty, and contradiction.
- Prediction Error and Salience Signal Ledger records learning pressure.
- Interactive Probing and regression calibration test memory behavior.
- Metamemory Answer Gate blocks unsafe fluent answers.
- Score Geometry provides traceable multi-dimensional decision evidence.

Those pieces are necessary but insufficient by themselves. Without a self-model, known failure patterns, calibration health, and answer posture selection, each subsystem can make local uncertainty decisions that are individually plausible but globally inconsistent.

## Architecture Decision

Add Cognitive Self-Regulation as an explicit project-aware control layer:

```text
workspace state
  + self-model
  + competence profiles
  + known failure patterns
  + calibration health
  + risk/source/context/belief evidence
  + score geometry
  -> self-regulation assessment
  -> answer posture / required operations
  -> attention routing and metamemory answer gate
  -> outcome feedback into calibration, signals, review, probes, replay, and self-model proposals
```

This layer is not allowed to own durable truth. It can recommend actions, publish evidence, create review/probe/regression candidates, and submit governed mutation candidates. It cannot bypass source anchors, access/redaction policy, review policy, mutation authority, or projection invalidation rules.

## Sequencing Decision

The patch suggested adding subbundles 21-26 after the current architecture integration closure. That ordering is too loose. Self-Regulation must be connected to execution gates:

1. `13a-probing-core-regression-calibration` must exist first because self-regulation needs probe outcomes and calibration records.
2. `21-cognitive-self-model` defines scoped competence, limits, known failure patterns, and policy profiles.
3. `23-calibration-health-and-probing-training` aggregates calibration evidence before behavior can be tuned.
4. `22-self-regulation-orchestrator` consumes self-model and calibration health to produce assessments and postures.
5. `24-professor-review-escalation` provides governed challenge/audit escalation from those assessments.
6. `19-metamemory-abstention-calibration` is reopened as a dependent gate that consumes assessment and posture.
7. `13-interactive-memory-probing-workbench`, `25-self-regulation-ui`, and `12-epistemic-drive-engine` consume the self-regulation outputs.
8. `26-cognitive-self-regulation-integration-closure` verifies the patch before cross-project and distributed extensions.

## Neuroscience-Inspired Rationale

The design maps self-regulation to metacognitive control, not consciousness. Useful biological inspiration is limited to:

- confidence calibration from prediction/outcome mismatch,
- task-dependent control policy,
- known failure pattern avoidance,
- inhibition of plausible but context-wrong memories,
- explicit uncertainty posture,
- replay and probing as training loops.

The implementation must avoid anthropomorphic claims. The engineering target is a traceable control system that can decide when to act, ask, audit, probe, review, escalate, learn, or abstain.

## Risks

- If self-model becomes prompt persona, it will drift and become unauditable.
- If display confidence drives posture, the architecture regresses to scalar-only behavior.
- If professor review is trusted as source truth, the system will outsource hallucination rather than reduce it.
- If calibration updates silently retune thresholds from one event, behavior will become unstable.
- If self-regulation bypasses mutation authority, the existing claim/evidence/belief safeguards are undermined.

## Required Bundle Repairs

- Add architecture files 27-30 and self-regulation diagrams 19-22.
- Add `CognitiveMemory.SelfRegulationContracts.cs`.
- Patch score geometry, neuro patch, and probing contract sketches.
- Add FR-055 through FR-061 and NFR-037 through NFR-041.
- Add subbundles 21-26 with full prerequisites, dependency impact, proof, and progression gates.
- Update `19-metamemory-abstention-calibration` so it depends on self-regulation orchestration.
- Update phase plan, traceability, validation, prompts, execution report, workbook, and manifest.
