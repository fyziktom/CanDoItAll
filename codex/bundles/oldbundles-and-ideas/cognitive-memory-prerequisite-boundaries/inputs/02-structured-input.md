# Structured Input

## Objective

Prepare a prerequisite refactor bundle that enables Cognitive Memory architecture without implementing Cognitive Memory.

## Required Outcome

- Add a MAF context contribution boundary so memory context can be supplied through extension contracts.
- Add source snapshot/read-model contracts so Workbench, Process, and Workflow sources can be consumed through stable, deterministic, source-grounded adapters.
- Keep existing behavior compatible.
- Project these prerequisites back into the Cognitive Memory architecture.

## Non-Goals

- Do not implement Cognitive Memory.
- Do not build source ingestion, recall, consolidation, projection, or UI.
- Do not redesign Workbench, Process, Workflow, RAG, or SemanticCompletion ownership.
- Do not add RAG typed filters here; that belongs to the Cognitive Memory adapter/projection phase.

## Success Signals

- Subbundles are small, ordered, and implementation-ready.
- Each subbundle has exact source references.
- The dependency impact is explicit.
- The Cognitive Memory bundle can depend on these boundaries instead of private internals.
