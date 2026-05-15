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

## MAF Handoff Use

Handoff can route memory work to specialists:

```text
Memory Manager Agent
  -> Source Curator Agent
  -> Procedure Miner Agent
  -> Contradiction Analyst Agent
  -> Projection Builder Agent
  -> Memory QA Agent
```

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
- `memory_delete_raw_source` should not exist in V1.

## Existing Code Touchpoints

- `MafAgentRuntime.Capabilities.Context.cs`: add memory context provider registration.
- `AgentFrameworkModuleServiceCollectionExtensions.cs`: register context provider factory, tools, workflow executors.
- `WorkflowExecutorIds`: add memory executor ids.
- `BuiltInWorkflowExecutorDescriptors`: add descriptors.
- `PersistentWorkflowRunStore`: no change required, but recall traces should link to workflow run ids.
- `ProcessRunAutomationDispatchService`: add reflection hook after governed outcome.
