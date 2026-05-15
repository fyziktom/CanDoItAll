# Neuroscience and Platform Notes

This architecture uses neuroscience as a design analogy. It is not a biological simulation.

## Neuroscience-Inspired Ideas Used

| Idea | System interpretation |
|---|---|
| Hippocampal indexing | Mindmap/project graph acts as a contextual navigation index. |
| Systems consolidation | Idle/night jobs reorganize recent events into durable semantic/procedural memory. |
| Replay during sleep | Recent workflows and episodes are revisited, summarized, linked, and projected. |
| Episodic vs semantic memory | Process/workflow events are distinct from generalized project knowledge. |
| Procedural memory | Successful workflows/procedures become reusable action patterns. |
| Attention/focus | Recall starts broad and narrows before retrieving detail. |
| Forgetting | Derived projections become dormant/stale/superseded without deleting raw evidence. |
| Metamemory | The system tracks what it knows, uncertainty, gaps, and where evidence may exist. |

## Platform Notes

- Qdrant should be treated as a projection layer. Payload filtering is central for safe retrieval by project, source, type, validation state, and scope.
- Microsoft Agent Framework workflows and handoff/orchestration concepts are used as the execution layer for recall/consolidation/reflection workflows.
- Existing CanDoItAll storage, process, workflow, plugin, and workbench modules should be reused to avoid duplicating source truth.

## Citation Guidance for External Documentation

When this bundle is copied into internal documentation, cite:

- neuroscience sources for systems consolidation, hippocampal indexing, and sleep-related memory consolidation,
- Qdrant documentation for payload/filter capabilities,
- Microsoft Agent Framework documentation for workflows, handoff, orchestration, and checkpointing semantics.
