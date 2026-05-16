# 12 Epistemic Drive Engine

## Status

- Ready after score geometry, recall traces, consolidation, human review UI, MAF integration, and probing-core evidence records are available.

## Objective

- Add the Epistemic Drive layer that models knowledge gaps as multi-dimensional, source-grounded, human-reviewable learning opportunities.

## Covered Inputs

- Requirements FR-024 through FR-031 and NFR-014 through NFR-019.
- `architecture/14-epistemic-drive-and-learning-orchestration.md`.
- `contracts/csharp/EpistemicDriveContracts.cs`.
- Recall traces, durable probe evidence/regression/calibration records, workflow/process runs, user corrections, source records, contradiction records, stale records, and project direction records.

## Numbering Note

- Root and plan subbundle folders both use `12-epistemic-drive-engine`. Dependency order is still controlled by `plan/01-phase-plan.md`; do not start this phase before the probing core has durable evidence records when probe outcomes are part of learning decisions.

## Prerequisites

- `05-recall-orchestrator` must persist trace evidence and feedback.
- `06-consolidation-engine` must support idempotent nightly/manual scans.
- `08-human-review-ui` must support review actions.
- `07-maf-workflow-integration` must support approval-gated workflow orchestration.
- `16-prediction-error-salience-signals` must provide signal/error evidence.
- `17-temporal-replay-scheduler` must provide replay outcome evidence.
- `18-procedural-skill-memory-simulation` must provide procedure maturity and simulation labels where learning touches procedures.
- `01b-score-geometry-driver` must provide the `EpistemicNeed` score space and region/Pareto shape evaluation.
- `13a-probing-core-regression-calibration` must exist when probe evidence is consumed.
- `19-metamemory-abstention-calibration` must exist when answer-gate warnings/abstentions are consumed as gap evidence.
- `13-interactive-memory-probing-workbench` should exist when proposal actions include request-probing or probing-after-learning browser proof.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\14-epistemic-drive-and-learning-orchestration.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\EpistemicDriveContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\diagrams\10-epistemic-drive-flow.mmd
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\plan\subbundles\12-epistemic-drive-engine\README.md

## Deliverables

- Knowledge region, coverage map, gap, tension, proposal, task, and outcome records.
- Epistemic Drive scan inside consolidation.
- Human-reviewable learning proposal UI and audit events.
- MAF learning workflow entry points with approval gates.
- Probing question generation and probing feedback ingestion.

## Dependency Impact

- Consolidation gains `KnowledgeCoverageRefresh`, `EpistemicDriveScan`, and `LearningOpportunityReview`.
- Recall traces become evidence for gap analysis but do not mutate authoritative memory.
- Human review queue gains learning proposal decisions.
- MAF orchestrates approved learning tasks but Cognitive Memory owns durable records.
- Qdrant projections refresh only after durable records exist.

## Validation Depth

- Unit tests for vector preservation, score-geometry backing, Pareto selection, ROI estimates, and category classification.
- Score geometry tests proving `KnowledgeNeedVector` references generic score vectors/shapes and display priority is derived.
- Integration tests for trace evidence to proposal generation and approval-gated task planning.
- Negative tests for scalar-only collapse, missing source refs, unapproved external study, duplicate scans, high-risk draft promotion, and Qdrant outage.
- Browser proof for Night Reflection / Cognitive Briefing and proposal detail.

## Implementation Steps

- Add EF models/configurations for Epistemic Drive records.
- Add services from `EpistemicDriveContracts.cs`.
- Extend consolidation modes and reports.
- Extend MAF executors and approval gates.
- Add review/UI surfaces.
- Add tests and fixtures, including the Docker operational knowledge scenario.

## Do Not Do

- Do not implement autonomous external study.
- Do not use a single score as the core model.
- Do not implement Epistemic scoring outside the generic score geometry driver.
- Do not store facts only in Qdrant.
- Do not promote generated learning output without source refs and validation state.
- Do not leak project-private source evidence into cross-project proposals.

## Acceptance Checklist

- Knowledge need vectors preserve all dimensions.
- Knowledge need vectors are backed by score vector snapshots and evaluation traces.
- Proposal explanations cite evidence refs.
- Human approval is required where policy requires it.
- Probing can be requested before learning and used after learning.
- Learning outcomes remain draft until validated.
- Projection refresh is traceable to durable memory changes.

## Proof Required

- Build/test proof.
- Sample Docker proposal evidence.
- Review UI screenshots.
- Audit/event records for approval decisions.
- Implementation report with any deviations.

## Browser Validation Logging

- Capture Night Reflection summary.
- Capture proposal detail with coverage map, evidence, sources, risks, and actions.
- Capture approval/snooze/request-probing result.

## Progression Gate

- Proceed to validation closure only after Epistemic Drive cannot create unapproved learning updates, scalar-only scoring is rejected by tests, generic score-geometry backing is proven, and proposal evidence is inspectable.

## Suggested Agent Prompt

- Implement the Epistemic Drive engine as an approval-gated, evidence-preserving, multi-dimensional learning proposal system. Keep durable memory authoritative and Qdrant rebuildable.
