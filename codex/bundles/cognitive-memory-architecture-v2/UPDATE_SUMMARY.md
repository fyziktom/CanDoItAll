# Architecture Update Summary

## Scope

This refreshed bundle adds Interactive Memory Probing, the neuro-cognitive architecture patch, the score-geometry foundation, and the Cognitive Self-Regulation patch to the Cognitive Memory architecture, then aligns the plan with the supplied current code snapshot and patch dependencies.

## Current Code Delta

The supplied current code already contains the two prerequisite boundaries that were previously only proposed:

- MAF context contribution boundary through contributor/policy/result contracts and runtime consumption.
- Source snapshot/evidence provider contracts for Workbench, Process runtime evidence, and Workflow runtime evidence.

Therefore this bundle treats those parts as target-branch validation items and focuses on consuming them.

## Added Architecture

- `architecture/15-interactive-memory-probing.md` defines the Dialogue Workbench, probe lifecycle, feedback model, and safe correction rules.
- `architecture/16-probing-regression-and-calibration-loop.md` defines regression tests, calibration metrics, and learning validation loops created from probe failures.
- `architecture/17-neuro-cognitive-integration-layer.md` through `architecture/24-metamemory-confidence-and-abstention.md` add cognitive workspace, attention routing, prediction error, salience signals, claim/evidence/belief ledger, context binding, episodic replay, procedural skill memory, simulation safety, and answer gating.
- `architecture/26-score-geometry-driver.md` defines the generic score-space, vector, shape, scalar projection, and evaluation-trace foundation used by recall, attention, belief, salience, replay, probing, answer gating, Epistemic Drive, mindmap similarity, activation, and cross-project promotion.
- `architecture/27-cognitive-self-regulation-layer.md` through `architecture/30-professor-review-and-escalation.md` add self-model, competence profiles, known failure patterns, self-regulation assessment, humility triggers, answer posture, calibration health, and governed professor review.
- `contracts/csharp/InteractiveMemoryProbingContracts.cs` defines the service contracts, probe records, findings, feedback actions, and regression test records.
- `contracts/csharp/CognitiveMemory.NeuroPatchContracts.cs` defines neuro-cognitive architecture contract sketches with strongly typed decisions, states, kinds, and commands.
- `contracts/csharp/CognitiveMemory.ScoringContracts.cs` defines score geometry contract sketches.
- `contracts/csharp/CognitiveMemory.SelfRegulationContracts.cs` defines self-regulation architecture contract sketches.
- `subbundles/13-interactive-memory-probing-workbench/README.md` is the executable implementation workstream for Codex.
- `subbundles/01b-score-geometry-driver/README.md` is the foundation phase that must run before feature subbundles introduce behavior-affecting scoring.
- `subbundles/14-neuro-foundation-claim-evidence-ledger` through `subbundles/20-architecture-integration-closure` split the neuro-cognitive patch into dependency-aware phases.
- `subbundles/21-cognitive-self-model` through `subbundles/26-cognitive-self-regulation-integration-closure` split self-regulation into dependency-aware phases.
- `validation/probing-test-matrix.md` defines functional and non-happy-path proof obligations.
- `validation/neuro-patch-test-plan.md` preserves the patch-specific proof matrix and Docker context-separation golden scenario.
- `validation/self-regulation-test-matrix.md` defines self-regulation scenario and negative proof obligations.

## Safety Rule

Probe feedback is evidence, not direct truth mutation. User corrections create review items, correction candidates, knowledge-gap evidence, and regression tests. Active memory still changes only through Cognitive Memory authority services and policy gates.

The neuro-cognitive patch strengthens this rule: authoritative memory changes go through mutation authority, working memory is not source truth, salience/replay/simulation do not create truth, and the answer gate must expose uncertainty before unsafe answers leave the system.

The score-geometry update strengthens prioritization and confidence rules: behavior-affecting scores must be score vectors/shapes with evaluation traces. Scalar scores are derived projections for display, sorting, queues, or tie-breaking only.

The Cognitive Self-Regulation update strengthens answer behavior and learning control: self-model, professor review, calibration outcomes, salience, prediction error, probing feedback, and generated summaries are evidence/control inputs only. They cannot directly create canonical truth or bypass source/access/review/mutation policy.

## Recommended Next Implementation Slice

Run `00-prerequisite-boundary-gate`, `01-module-foundation`, `01a-common-drivers-helpers-and-ef-guardrails`, `01b-score-geometry-driver`, and then `14-neuro-foundation-claim-evidence-ledger` before source ingestion. Build workspace/attention and signal ledgers before recall. Implement probing core before self-model/calibration health. Reopen answer gating after self-regulation orchestration and professor review are available. Implement Epistemic Drive only after signals, replay, probing, answer-gate, and self-regulation evidence are available.
