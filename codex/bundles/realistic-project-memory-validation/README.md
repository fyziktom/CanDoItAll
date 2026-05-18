# Realistic Project Memory Validation

This bundle converts two detailed real-world project packs into time-sliced CanDoItAll project structures, ingests them into Cognitive Memory, and validates whether recall returns source-grounded, useful project context.

## Profile

- `initiative`

## Mission

- Build two structured project hierarchies from the AI Tap and Curacao glass recycling source packs, load them only through CanDoItAll APIs, then run multi-cycle Cognitive Memory ingestion, review, consolidation, and recall checks against the normalized source truth.

## Outcome Contract

- Requested outcome: create deep, time-ordered project structures for both source packs and prove Cognitive Memory can recall accurate contextual packs from them.
- Hard constraints: source data remains in bundle/source artifacts; no source-pack content is added to app code; all project data is loaded through APIs; each project has at least four time-based source-truth groups.
- Evidence required before closure: prepared bundle validation, API load evidence, project-structure readbacks, ingestion/consolidation snapshots, review decisions, recall probes, and source-truth comparison analysis.
- Known blockers or explicit scope exceptions: implementation repair is only in scope if API evidence identifies an actionable Cognitive Memory defect.

## Bundle Layout

- `inputs/` raw request, source artifact inventory, extraction output, and structured input
- `source-truth/` normalized time-sliced source truth, source manifest, and mindmap
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and API boundaries
- `plan/` execution order and dependency gates
- `traceability/` requirement-to-subbundle mapping
- `validation/` API runner, memory-quality analyzer, and evidence output
- `subbundles/` execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-source-extraction-and-truth-structuring`
2. `subbundles/02-project-structure-api-load`
3. `subbundles/03-cognitive-memory-ingestion-and-consolidation-validation`
4. `subbundles/04-recall-probing-and-implementation-repair`

## Dependency And Validation Map

- Source extraction and source-truth normalization are the foundation.
- API loading must read from `source-truth/source-manifest.json`; it must not scrape the raw source folder at runtime.
- Cognitive Memory validation must run after project-structure readback proves the nested nodes and links exist.
- Implementation repair is gated by failed recall or consolidation evidence, not speculation.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Ready for API execution`
- Subbundle gate review: `Prepared-stage gates defined`
- Final closure gate: `Pending API evidence and memory-quality analysis`
- Browser validation analytics: `N/A; API and file evidence only`
