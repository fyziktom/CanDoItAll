# Structured Input

## Primary Goal

Upgrade Cognitive Memory so it can reliably turn raw/source-derived memories into useful, validated, clustered, aggregated, and consumer-ready knowledge.

## Functional Needs

- Multi-key clustering across semantic, source, project, temporal, evidence, entity, task, and contradiction/supersession dimensions.
- Explicit dreaming runs that inspect clusters, produce aggregate candidates, resolve duplicates, identify contradictions, and record quality metrics.
- Aggregate memories with claim-level provenance and source/evidence mappings.
- Validation gates that make shallow or suspiciously fast dream completion visible and prevent low-quality aggregates from silently becoming active memories.
- Retrieval synthesis that converts selected memories into concise briefings while keeping diagnostics and references available on demand.

## Non-Goals

- No economic memory management.
- No attention market, memory prices, memory loans, or governance economy.
- No unrelated rewrite of the Cognitive Memory UI.
- No autonomous daemon that mutates memory without explicit run records, auditability, and operator controls.

## Quality Bar

- Every aggregate sentence must be traceable to one or more source memories, source items, claims, or evidence anchors.
- Generated synthesis must be clearly marked as generated synthesis, not original source truth.
- The system must support both concise agent-facing context and diagnostic drill-down views.
- Tests must cover duplicates, contradictions, temporal updates, access/redaction, and cross-project boundaries.
