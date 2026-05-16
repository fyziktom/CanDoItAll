# 18 Cognitive Workspace And Attention Router

## Purpose

Separate active cognition from stored memory.

The current architecture has `RecallContextPack`, but a context pack is a rendered product. A cognitive workspace is an active state container that exists while an agent, user, workflow, process step, or probing session is operating.

## Cognitive Workspace

A workspace frame should be scoped to one of:

- user conversation,
- agent run,
- workflow run,
- process step,
- probe session,
- review session,
- learning task.

## Workspace Frame Contents

| Field | Meaning |
|---|---|
| `WorkspaceFrameId` | Stable id for the active frame. |
| `ProjectId` | Project scope. |
| `OwnerUserId` / `OwnerAgentId` | Actor using the frame. |
| `ProcessRunId` / `WorkflowRunId` / `ProbeSessionId` | Optional execution scope. |
| `GoalStack` | Active goals and subgoals. |
| `FocusSlots` | Limited active items/claims/procedures. |
| `InhibitedCandidates` | Relevant-but-blocked candidates and reasons. |
| `OpenQuestions` | Unknowns that need clarification/probing/source audit. |
| `ContextBudget` | Token/section/detail budget. |
| `CognitiveLoadTrace` | Score-geometry evaluation of saturation and missing dimensions; helps decide summarization/abstention. |
| `LastAttentionDecision` | What operation was selected and why. |
| `ExpiresAtUtc` | Working frames are temporary by default. |

## Focus Slots

A focus slot can point to:

- memory item,
- atomic claim,
- procedure skill,
- source item,
- recall trace,
- probe turn,
- workflow artifact,
- unresolved question,
- external-source placeholder.

Each slot should store:

- attention score vector and derived display projection,
- reason for inclusion,
- source sufficiency,
- risk level,
- confidence,
- staleness,
- relation to active goal,
- optional compression summary.

## Inhibition

The workspace must explicitly represent inhibited candidates, because wrong but semantically close context is one of the main failure modes.

Example:

```text
Candidate: Docker test simulation procedure
Query: production deployment procedure
Action: inhibited
Reason: semantically related but context-separated; environment=test-simulation, target=production
```

Inhibition is not deletion. It is a local attention decision.

## Attention Router

The attention router decides the next operation from current context.

### Inputs

- user/agent request,
- current workspace frame,
- process/workflow goal,
- recall trace signals,
- source sufficiency,
- cognitive signal vector,
- prediction error history,
- access/redaction policy,
- risk profile,
- available tools/workflows.

The router must evaluate these inputs through the `AttentionRouting` score space. Operation choices are shape matches such as recall, answer-from-workspace, clarification, source audit, probe, review, learning proposal, replay, or abstention. A scalar projection can help order candidate operations, but the routing decision must persist the vector dimensions, matched shape, missing dimensions, and explanation.

### Decision Types

| Decision | Use when |
|---|---|
| `Recall` | There is likely enough memory to answer but relevant memory is not loaded. |
| `AnswerFromWorkspace` | Workspace already contains enough source-backed focus. |
| `AskClarification` | Scope/intent/context is ambiguous. |
| `RunSourceAudit` | Answer requires source proof or source freshness. |
| `StartProbe` | The topic is weak and interrogation is more efficient than study. |
| `CreateReviewItem` | Conflict, correction, or risky memory mutation needs review. |
| `RequestLearningProposal` | Missing knowledge is relevant and study may be useful. |
| `RunReplay` | Weak/stale/high-risk memory should be rehearsed or regression-tested. |
| `Abstain` | Answer would be unsafe, unsourced, or misleading. |

## Relationship To Recall

Recall should be a tool used by attention, not the whole cognitive loop.

Recommended flow:

```text
request
  -> load/create workspace frame
  -> attention router chooses Recall
  -> recall orchestrator returns candidates/trace/context pack
  -> workspace updates focus and inhibition
  -> metamemory answer gate decides answer/warn/ask/abstain
  -> output rendered
  -> prediction/error/salience signals recorded after feedback or outcome
```

## Relationship To Quick Responses

This layer enables the "short immediate reaction, detailed answer later" behavior:

1. Attention router can produce an immediate low-detail response from active workspace.
2. It can schedule deeper recall/source audit/consolidation.
3. It can render a second-stage answer once evidence is loaded.

The system must not pretend the quick response is fully verified. It should label it as preliminary when necessary.

## Persistence Rules

- Workspace frames are ephemeral by default.
- Important frames can be persisted as episodic source input when they affect decisions, corrections, or workflow outputs.
- Workspace content is not source truth.
- Workspace can reference claims and sources, but cannot promote claims directly.

## Required Updates To Existing Docs

- `architecture/05-recall-orchestrator.md`: add workspace frame and attention decision to recall trace.
- `architecture/15-interactive-memory-probing.md`: probe sessions should own or attach to a workspace frame.
- `architecture/08-maf-workflow-agent-integration.md`: agent context contribution should come from workspace-aware recall, not raw recall alone.
- `requirements/01-normalized-requirements.md`: add requirements for working frame and attention decisions.
- `validation/test-and-quality-plan.md`: add tests for inhibition and context-budget behavior.
