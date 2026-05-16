# 12 Epistemic Drive Engine

## Status

- Ready after recall traces, consolidation, human review UI, and MAF integration are available.

## Objective

- Add the Epistemic Drive layer that models knowledge gaps as multi-dimensional, source-grounded, human-reviewable learning opportunities.

## Covered Inputs

- Requirements FR-024 through FR-031 and NFR-014 through NFR-019.
- `architecture/14-epistemic-drive-and-learning-orchestration.md`.
- `contracts/csharp/EpistemicDriveContracts.cs`.
- Recall traces, probing sessions, workflow/process runs, user corrections, source records, contradiction records, stale records, and project direction records.

## Numbering Note

- Root `subbundles/11-validation-and-architecture-closure` already exists, so this mirrored execution subbundle uses `12-epistemic-drive-engine`. The plan subbundle remains `plan/subbundles/11-epistemic-drive-engine` because the plan folder had no validation-closure entry.

## Prerequisites

- `05-recall-orchestrator` must persist trace evidence and feedback.
- `06-consolidation-engine` must support idempotent nightly/manual scans.
- `08-human-review-ui` must support review actions.
- `07-maf-workflow-integration` must support approval-gated workflow orchestration.
- Probing contracts should exist or be explicitly staged as a dependency.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\architecture\14-epistemic-drive-and-learning-orchestration.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\contracts\csharp\EpistemicDriveContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\diagrams\10-epistemic-drive-flow.mmd
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\plan\subbundles\11-epistemic-drive-engine\README.md

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

- Unit tests for vector preservation, Pareto selection, ROI estimates, and category classification.
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
- Do not store facts only in Qdrant.
- Do not promote generated learning output without source refs and validation state.
- Do not leak project-private source evidence into cross-project proposals.

## Acceptance Checklist

- Knowledge need vectors preserve all dimensions.
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

- Proceed to validation closure only after Epistemic Drive cannot create unapproved learning updates, scalar-only scoring is rejected by tests, and proposal evidence is inspectable.

## Suggested Agent Prompt

- Implement the Epistemic Drive engine as an approval-gated, evidence-preserving, multi-dimensional learning proposal system. Keep durable memory authoritative and Qdrant rebuildable.
