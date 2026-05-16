# Microsoft Agent Framework Integration

## Purpose

MAF is the executive-control layer: workflows, agents, tools, handoffs, approvals, and runtime context. Cognitive Memory should provide memory services to MAF but remain durable and module-owned.

## Integration Surfaces

### 1. Context Provider

Add a cognitive context provider similar to existing `WorkspaceMemoryContextProvider`, but backed by `IRecallOrchestrator`.

Behavior:

```text
MAF agent invocation
  -> CognitiveMemoryContextProvider receives request messages
  -> builds RecallRequest from agent/process/workflow context
  -> calls IRecallOrchestrator
  -> renders compact RecallContextPack
  -> injects system/developer context
```

### 2. Agent Tools

Expose safe tools:

- `memory_recall`
- `memory_get_source_detail`
- `memory_record_episode`
- `memory_record_decision`
- `memory_record_reflection`
- `memory_request_review`
- `memory_mark_used`
- `memory_epistemic_scan`
- `memory_learning_propose`
- `memory_learning_plan`
- `memory_learning_execute_approved`
- `memory_learning_submit_outcome`
- `memory_probing_generate_questions`

Dangerous mutations should require policy/approval.

### 3. Workflow Executors

Add workflow executors:

| Executor id | Purpose |
|---|---|
| `memory.recall` | Retrieve a context pack for a process/workflow step. |
| `memory.source.ingest` | Ingest a source or source scope. |
| `memory.project` | Project canonical memory to Qdrant/search. |
| `memory.consolidate` | Run consolidation for project/global scope. |
| `memory.reflect` | Convert process/workflow output into episode/reflection records. |
| `memory.review.enqueue` | Create human review tasks. |
| `memory.epistemic.scan` | Run coverage/gap/tension analysis for an approved scope. |
| `memory.learning.propose` | Create or refresh human-reviewable learning proposals. |
| `memory.learning.plan` | Convert an approved proposal into a scoped learning task. |
| `memory.learning.execute.approved` | Run an approved source study workflow. |
| `memory.learning.qa` | Verify source refs, risk state, and draft output before promotion. |
| `memory.probing.generate` | Generate probing questions for a knowledge region or proposal. |

### 4. Process Reflection Hook

After a process step or workflow run completes:

```text
Process/Workflow completed
  -> MemoryReflectionService receives event
  -> creates episodic candidate
  -> extracts decisions/procedures if safe
  -> queues consolidation
```

### 5. Workflow Templates

Create default workflow templates:

- `Project Memory Nightly Consolidation`
- `Mindmap Source Ingestion`
- `Process Run Reflection`
- `Qdrant Projection Rebuild`
- `Contradiction Review`
- `Procedure Mining`
- `Night Reflection / Epistemic Drive Scan`
- `Learning Proposal Review`
- `Approved Source Study`
- `Learning Outcome QA`

## MAF Handoff Use

Handoff can route memory work to specialists:

```text
Memory Manager Agent
  -> Source Curator Agent
  -> Procedure Miner Agent
  -> Contradiction Analyst Agent
  -> Epistemic Drive Agent
  -> Knowledge Gap Analyst Agent
  -> Learning Planner Agent
  -> Source Study Agent
  -> Learning QA Agent
  -> Projection Builder Agent
  -> Memory QA Agent
```

## Learning Workflow Boundary

MAF may orchestrate learning tasks, but Cognitive Memory owns durable memory state.

Required boundary:

```text
Learning proposal approved by human/policy
  -> MAF runs Learning Planner Agent
  -> MAF runs approved Source Study Agent if source policy allows it
  -> MAF runs Procedure/Runbook Miner Agent and Learning QA Agent
  -> agents submit draft records and report
  -> Cognitive Memory validates source refs and policy
  -> Cognitive Memory writes durable draft/approved records
  -> Projection manager refreshes Qdrant/search projections
```

MAF agents must not directly write canonical memory, proposal decisions, or projections.

## Context Pack Rendering Rules

For agent runs, context packs should be compact:

```text
Relevant project memory:
1. [Decision] Test Docker is separate from production Docker. Source: ... Confidence: ...
2. [Procedure] Run test simulation Docker. Source: ... Confidence: ...
3. Related but separate: Production Docker deployment. Do not mix configs.
Open uncertainty: Qdrant projection version may be stale.
Available detail tools: memory_get_source_detail(...)
```

## Governance

- `memory_recall` can be low-risk read-only.
- `memory_record_episode` can be allowed for agents but should be traceable.
- `memory_record_decision` should require confidence/source refs or human review.
- `memory_consolidate` should require project-level permission.
- `memory_projection_rebuild` can be background/admin only.
- `memory_epistemic_scan` can run under project-level consolidation permission.
- `memory_learning_propose` creates reviewable proposals only.
- `memory_learning_execute_approved` requires recorded approval and approved source scope.
- `memory_learning_submit_outcome` creates draft output until QA/human review accepts it.
- `memory_delete_raw_source` should not exist in V1.

## Existing Code Touchpoints

- `MafAgentRuntime.Capabilities.Context.cs`: add memory context provider registration.
- `AgentFrameworkModuleServiceCollectionExtensions.cs`: register context provider factory, tools, workflow executors.
- `WorkflowExecutorIds`: add memory executor ids.
- `BuiltInWorkflowExecutorDescriptors`: add descriptors.
- `PersistentWorkflowRunStore`: no change required, but recall traces should link to workflow run ids.
- `ProcessRunAutomationDispatchService`: add reflection hook after governed outcome.

## Probing Workflow And Tool Integration

Add optional MAF/workflow wrappers for probing:

- `memory.probe.session.start`
- `memory.probe.ask`
- `memory.probe.generateQuestions`
- `memory.probe.feedback`
- `memory.probe.regression.create`
- `memory.probe.regression.run`
- `memory.probe.learning.validate`

These wrappers call Cognitive Memory services. MAF does not own probe state and must not directly mutate memory records. Probing tools should be especially careful with access context, redaction, and generated source explanations.

## Workspace-Aware Agent Context

MAF context contribution should become workspace-aware:

```text
agent/workflow request
  -> Cognitive Memory loads or creates workspace frame
  -> attention router decides recall/source audit/probe/clarification/abstention
  -> recall fills workspace focus slots and inhibition records
  -> metamemory answer gate decides what may be rendered
  -> MAF receives compact context pack and available detail tools
```

MAF must not own workspace persistence, attention policy, mutation authority, prediction errors, salience signals, or answer-gate decisions. It may pass run ids and access context into Cognitive Memory and receive trace ids for audit.

Additional wrapper tools/executors may be added only after their backend services exist:

- `memory.workspace.open`
- `memory.attention.route`
- `memory.claim.propose`
- `memory.replay.enqueue`
- `memory.answerGate.evaluate`
- `memory.procedureSkill.propose`
- `memory.simulation.create`

All write-capable wrappers submit commands to Cognitive Memory mutation authority or dedicated application services. They must never write canonical memory, claims, procedure skills, replay results, or projections directly.
