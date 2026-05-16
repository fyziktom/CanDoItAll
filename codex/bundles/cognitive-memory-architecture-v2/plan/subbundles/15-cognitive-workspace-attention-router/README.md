# 15 Cognitive Workspace Attention Router

## Status

- Ready after `01b-score-geometry-driver`, `04-memory-taxonomy-and-projections`, and `14-neuro-foundation-claim-evidence-ledger`.
- Critical foundation for recall, probing, MAF, and answer gating.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.
## Objective
Add active working-memory frames and explainable attention routing so recall/probing/MAF flows know what is currently in focus and which operation should happen next.

## Covered Inputs

- Neuro patch FR-039, FR-040, FR-052 and NFR-028.
- Patch findings C-01 and H-01.
- Existing v2 recall, probing, MAF, and context-pack architecture.

## Prerequisites

- `14-neuro-foundation-claim-evidence-ledger` provides context frames, claims, evidence anchors, and mutation authority.
- `01b-score-geometry-driver` provides attention-routing and workspace-focus score spaces and evaluation traces.
- `02-workbench-and-source-ingestion` and `04-memory-taxonomy-and-projections` have enough source/projection state to test workspace focus and inhibition.
- `03-semantic-and-rag-adapters` is available for candidate sources but not required for correctness.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\17-neuro-cognitive-integration-layer.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\18-cognitive-workspace-and-attention-router.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\05-recall-orchestrator.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\08-maf-workflow-agent-integration.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\15-interactive-memory-probing.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.NeuroPatchContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\test-and-quality-plan.md

## Deliverables

- Cognitive workspace frame model and services.
- Focus slot, goal stack, open question, context budget, cognitive load, and inhibition records.
- Attention router request/decision model.
- Trace contract updates for workspace frame id and attention decision id.
- MAF and probing rules for attaching to workspace frames.

## Dependency Impact

- Recall starts from workspace/attention rather than directly rendering a context pack.
- MAF context contribution becomes workspace-aware.
- Probing sessions attach to workspace frames.
- Metamemory answer gate consumes workspace and attention decisions.
- Epistemic Drive can consume workspace open questions as gap evidence.

## Validation Depth

- Unit tests for workspace lifecycle, expiry, focus slots, goal stack, open questions, cognitive load, and context budget.
- Integration tests for attention decisions: recall, answer from workspace, clarification, source audit, probe, review, learning proposal, replay, and abstention.
- Trace tests proving selected and inhibited candidates include structured reasons.
- Negative tests proving workspace content is not source truth.
- EF/performance review for active workspace and trace query paths.
- Score geometry tests for attention operation shapes, cognitive-load evaluation, and candidate inhibition traces.

## Implementation Steps

1. Add workspace and attention contracts/entities/configurations.
2. Add application services for get/create/update workspace and route attention.
3. Add trace hooks for workspace and attention decision ids.
4. Add deterministic routing fixtures for ambiguity, weak topics, source audit, high-risk procedure, and Docker context boundaries.
5. Add MAF/probing integration seams without implementing full UI.

## Scope Exceptions

- Do not implement final recall ranking or answer rendering here.
- Do not implement salience ledger, prediction error engine, replay scheduler, or answer gate in this subbundle.

## Do Not Do

- Do not treat `RecallContextPack` as working memory.
- Do not persist workspace as source truth.
- Do not hide inhibition reasons inside free-text logs only.
- Do not let attention routing silently fallback to answer when clarification/source audit/abstention is required.

## Acceptance Checklist

- Workspace frames can be scoped to user conversation, agent run, workflow run, process step, probe session, review session, or learning task.
- Focus and inhibition records are durable/auditable where required.
- Attention decisions include structured reason, score vector, matched shape, missing dimensions, and scalar projection where needed.
- Trace records can reference workspace frame and attention decision ids.
- Context-budget behavior is testable.

## Proof Required

- Build/test output.
- EF model/index proof.
- Attention-routing fixture output.
- Trace sample showing focus and inhibited candidates.
- Implementation report with deviations.

## Browser Validation Logging

- N/A for backend foundation.
- Browser proof is required later where workspace/focus/inhibition appears in recall trace, Dialogue Workbench, review, or answer-gate UI.

## Progression Gate

- Do not proceed to recall/probing/MAF answer integration until workspace frames and attention decisions are traceable and tested.
- Reopen this subbundle if downstream flows bypass attention routing, cannot explain inhibited candidates, or introduce untyped attention score breakdowns.

## Suggested Agent Prompt

Implement Cognitive Workspace and Attention Router as the active control layer for Cognitive Memory. Keep context packs as rendered outputs, make inhibition explicit, and record attention decisions before recall/probing/MAF flows act.
