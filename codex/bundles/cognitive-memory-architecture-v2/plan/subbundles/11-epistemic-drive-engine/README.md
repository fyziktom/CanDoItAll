# Subbundle 11-epistemic-drive-engine

## Objective

Add the Epistemic Drive vertical slice: coverage maps, gap detection, multi-dimensional tension modeling, human-reviewable learning proposals, probing question generation, and approved learning task planning.

## Scope

- Knowledge regions and coverage maps.
- Knowledge gap records and evidence refs.
- `KnowledgeNeedVector` preservation.
- Epistemic tension evaluation using Pareto/category/ROI methods.
- Learning proposal lifecycle and review actions.
- Probing-before-learning and probing-after-learning integration.
- Learning task planning for approved sources only.

## Inputs

- `architecture/14-epistemic-drive-and-learning-orchestration.md`
- `architecture/03-memory-taxonomy-and-data-model.md`
- `architecture/05-recall-orchestrator.md`
- `architecture/06-consolidation-engine.md`
- `architecture/08-maf-workflow-agent-integration.md`
- `architecture/10-security-governance-and-provenance.md`
- `architecture/11-ui-and-operator-experience.md`
- `contracts/csharp/EpistemicDriveContracts.cs`
- recall traces, consolidation runs, probing sessions, workflow/process failures, user corrections, source records, and project graph records.

## Required Code Areas

- Cognitive Memory domain/EF models.
- Consolidation pipeline.
- Recall trace evidence reader.
- Human review queue.
- MAF workflow executor registration.
- Operator UI components.
- Probing session contracts when available.
- Projection refresh only after durable memory updates.

## Implementation Rules

- Do not collapse Epistemic Drive into a scalar-only score.
- Preserve vector components, evidence refs, category, ROI estimate, and explanation.
- Do not run external source study without required approval.
- Do not let learning output become source truth.
- Every learning-derived canonical record requires source refs.
- Do not mutate authoritative memory from distributed workers.
- Keep source code comments in English.
- Use typed enums/options for modes and decisions.

## Suggested Vertical Slice

1. Add durable coverage map, gap, tension, proposal, and task records.
2. Read recall traces, workflow failures, stale records, contradictions, and user corrections into typed evidence refs.
3. Run `EpistemicDriveScan` during manual/project-nightly consolidation.
4. Generate a Docker operational knowledge proposal with coverage map and probing questions from a fixture.
5. Show the proposal in the review UI with approve, reject, snooze, narrow scope, and request probing actions.
6. Convert an approved proposal into a planned learning task without executing external study yet.

## Data Model Tasks

- Add EF entities/configurations for knowledge regions, coverage maps, gap records, tension records, learning proposals, learning tasks, learning outcomes, open question sets, and probing question sets.
- Store evidence refs for recall traces, workflow run ids, source item ids, canonical memory item ids, contradiction ids, probing session ids, user correction ids, and project direction ids.
- Store algorithm/profile versions, input hashes, and policy decisions.

## Service And Interface Tasks

- Implement `IKnowledgeCoverageService`.
- Implement `IKnowledgeGapDetector`.
- Implement `IEpistemicDriveEngine`.
- Implement `ILearningProposalService`.
- Implement `ILearningTaskPlanner`.
- Add consolidation modes `KnowledgeCoverageRefresh`, `EpistemicDriveScan`, and `LearningOpportunityReview`.
- Add MAF executors for scan, proposal review handoff, task planning, and approved learning workflow start.

## UI Tasks

- Add Night Reflection / Cognitive Briefing panel.
- Add knowledge coverage map detail.
- Add learning proposal detail with evidence, project direction intersections, suggested sources, risks, depth, and acceptance criteria.
- Add approve, reject, snooze, narrow scope, expand scope, add source, request probing, convert to bundle, and assign actions.
- Show scalar display priority only as a secondary sorting value.

## Tests

- Unit tests for vector component preservation and category classification.
- Unit tests for Pareto candidate selection and ROI estimation.
- Integration tests for recall-trace evidence to proposal creation.
- Integration tests for consolidation scan idempotency and resume behavior.
- UI/browser tests for proposal review actions.
- Contract tests for MAF executor policy gates.

## Non-Happy Path Tests

- External source study blocked without approval.
- Proposal generated with insufficient source availability asks for sources or probing.
- High-risk procedure output remains draft pending human validation.
- Probing failure updates gap evidence without overwriting validated memory.
- Distributed worker output cannot mutate proposals or durable memory directly.
- Qdrant outage does not prevent durable proposal creation.
- Duplicate scan does not create duplicate proposals.

## Acceptance Criteria

- Epistemic tension is stored as a vector with evidence, not only as one score.
- Learning proposals explain why this topic, why now, weak subareas, project directions, sources, outputs, risks, and required approvals.
- Human approval, rejection, snooze, scope edits, and probing request decisions are persisted and audited.
- Approved learning tasks use only approved source scope.
- Learning outcomes create draft memory/procedure/probing records with source refs.
- Projection updates occur only after durable records exist.

## QA Questions

1. Can a reviewer reconstruct every proposal from evidence refs?
2. Does the implementation prevent scalar-only prioritization?
3. Are external study and high-impact memory updates approval-gated?
4. Are source refs mandatory for learning-derived canonical records?
5. Does probing feedback change gap evidence without becoming automatic truth?
6. Are cross-project signals policy-filtered?

## Evidence Required

- Build and relevant test output.
- EF migration/model proof.
- Sample Docker proposal fixture and stored evidence refs.
- Browser evidence for proposal review UI.
- Audit/event proof for approval and snooze decisions.
- Report of any architecture deviations.
