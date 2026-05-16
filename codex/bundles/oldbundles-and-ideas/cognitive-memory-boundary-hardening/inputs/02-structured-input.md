# Structured Input

## Objective

Prepare a focused refactor bundle that hardens the boundary contracts created by `cognitive-memory-prerequisite-boundaries`.

## Required Outcome

- Source providers support scalable, deterministic paging without materializing entire source sets before returning a page.
- Cursors are anchored and stale/invalid cursor behavior is explicit.
- Redaction and source hash rules are safe for future durable memory and vector projection.
- MAF contributor trace metadata is captured for future Cognitive Memory recall/context audit.
- Cognitive Memory architecture artifacts are synchronized so implementation agents see the hardening gate.

## Non-Goals

- Do not implement Cognitive Memory.
- Do not create memory entities, projection records, recall orchestration, consolidation jobs, or UI pages.
- Do not add Qdrant/RAG filtering.
- Do not change user-visible Workbench, Process, Workflow, or MAF behavior except for safer boundary metadata and trace recording.

## Success Signals

- Boundary tests cover invalid/stale cursors, large-page behavior, redaction, restricted hashes, and trace preservation.
- Existing context contributor and source snapshot tests continue to pass.
- The future Cognitive Memory `02-workbench-and-source-ingestion`, `05-recall-orchestrator`, and `07-maf-workflow-integration` subbundles can consume the hardened contracts without adding ad hoc safeguards.
