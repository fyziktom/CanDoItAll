# Assumptions And Risks

## Working Assumptions

- Existing MAF behavior must remain compatible while a context contribution extension point is added.
- Cognitive Memory implementation will start after this prerequisite bundle is accepted.
- Source snapshot contracts can be added without changing Workbench UI behavior or process/workflow persistence semantics.
- Source snapshot contracts should be read-only from the perspective of Cognitive Memory.
- Workbench Z coordinate support can remain metadata-backed for V1.

## Critical Path Risks

- If the MAF context boundary is skipped, Cognitive Memory will likely be hardwired into private runtime internals.
- If source snapshot contracts are skipped, ingestion will likely depend on ad hoc EF reads and unstable module internals.
- If the prerequisite grows into implementation, it will delay architecture approval and blur responsibility.
- If contracts are too generic, they will not carry enough provenance, hash, cursor, and layout information for reliable memory.
- If contracts are too Cognitive Memory-specific, existing modules will become reverse-coupled to a future feature.

## Validation Risks

- A compile-only pass can miss dependency-direction problems.
- A happy-path snapshot test can miss deletion, tombstone, stale cursor, and metadata-version behavior.
- MAF integration can appear to work while bypassing policy, trace, or contributor ordering.
- Process/workflow source contracts can omit enough detail to make episodic/procedural memory weak later.

## Reopen Triggers

- Cognitive Memory implementation requires editing private MAF context-provider logic directly.
- A source adapter must read Workbench, Process, or Workflow EF entities from outside the owning module.
- Source snapshots lack deterministic ids, content hashes, cursors, timestamps, or provenance.
- MAF receives a context pack without contributor trace or policy context.
- Existing Workbench, Process, Workflow, or MAF behavior changes unexpectedly.
