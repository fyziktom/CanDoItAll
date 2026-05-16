# Structured Input

## Objective

Prepare the Cognitive Memory architecture bundle for implementation planning, but do not implement the module.

## Required Architectural Outcome

- Design a cognitive memory module that supports multiple modes, high-volume operations, source-grounded canonical memory, explicit relations, staged recall, consolidation, human review, and distributed idle compute.
- Reuse the existing CanDoItAll application, Workbench, process/workflow runtime, RAG driver, Qdrant driver, and SemanticCompletion driver.
- Identify prerequisite refactors in existing code before Cognitive Memory implementation begins.
- If prerequisite refactors are required, create a separate detailed bundle for them using the CanDoItAll bundle workflow.

## Hard Constraints

- No implementation work in this round.
- Qdrant is a projection store, not durable memory.
- Raw source provenance and content hashes are mandatory.
- Generated summaries and context packs cannot become source truth.
- MAF is an executive-control integration layer, not the durable memory store.
- Distributed workers cannot directly mutate memory tables or Qdrant.
- Architecture must stay maintainable and strongly typed.

## Assumptions

- Cognitive Memory implementation will start after architecture approval.
- The first vertical slice should prove Workbench source ingestion, canonical memory, projection, recall, and traceability before distributed compute.
- Existing RAG and SemanticCompletion repos can be referenced or packaged for use by the main CanDoItAll solution.
- The main application remains the authority for project, process, workflow, storage, security, and plugin boundaries.

## Success Signals

- The bundle has a dependency-aware phase plan with critical foundations and progression gates.
- Subbundles are actionable and include exact source references.
- Scale, operational modes, and background workload behavior are explicit.
- Prerequisite refactor work is isolated from Cognitive Memory implementation and justified with source evidence.
