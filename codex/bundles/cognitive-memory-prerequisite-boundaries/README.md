# Cognitive Memory Prerequisite Boundaries

This bundle prepares the smallest required refactors before Cognitive Memory implementation starts.

## Profile

- `initiative`

## Mission

Create explicit extension boundaries so Cognitive Memory can integrate with MAF, Workbench, Process, and Workflow sources without hardwiring durable memory policy into private runtime internals or reading source tables ad hoc.

## Outcome Contract

- Requested outcome: detailed prerequisite refactor bundle only; no implementation in this round.
- Hard constraints: preserve existing behavior, keep changes narrowly scoped, avoid new abstractions unless they protect a real boundary, and keep Cognitive Memory implementation out of this bundle.
- Evidence required before closure: source-backed design, dependency map, exact source references, implementation-ready subbundles, and prepared-stage validation.
- Known blockers or explicit scope exceptions: RAG typed filters and Qdrant projection lifecycle belong to the Cognitive Memory bundle, not this prerequisite bundle.

## Why This Bundle Exists

The current architecture would otherwise push Cognitive Memory into the private MAF context builder and force source ingestion to depend on module internals. That is the wrong place for durable memory. Memory must be long-lived, traceable, testable, and reusable by UI, workflows, agents, and future connectors. The prerequisite work creates only the boundaries required to keep that architecture clean.

## Bundle Layout

- `inputs/` raw request, source artifacts, and structured input.
- `analysis/` current state, assumptions, and risks.
- `requirements/` normalized requirements.
- `architecture/` target prerequisite design.
- `plan/` dependency-aware phase plan.
- `traceability/` requirement-to-subbundle mapping.
- `shared-prompts/` reusable implementation and QA prompts.
- `subbundles/` execution-ready prerequisite workstreams.
- `reviews/` self-review and execution report.

## Recommended Execution Order

1. `subbundles/01-maf-context-contribution-boundary`
2. `subbundles/02-source-snapshot-read-models`
3. `subbundles/03-process-workflow-memory-event-boundaries`
4. `subbundles/04-validation-and-architecture-closure`

## Validation Summary

- Bundle preparation status: `Prepared for review`
- Execution status: `Not started`
- Subbundle gate review: `Seeded`
- Final closure gate: `Not started`
- Browser validation analytics: `Not applicable until implementation`
