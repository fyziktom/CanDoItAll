# Deep review checklist

## Source of truth
- Is each important concept owned by one canonical type?
- Is the same truth persisted in multiple places?
- Are projections writable?
- Are snapshots confused with live state?

## Identity
- Which concepts have identity?
- Are identities stable and explicit?
- Are there hidden composite keys or implicit keys?

## Relations
- Are many-to-many links explicit?
- Who owns dependency semantics?
- Are parent-child and dependency semantics distinct?
- Are relation invariants enforced anywhere?

## Time semantics
- Which concepts are time-based?
- Are due dates, spans, and history clearly separated?
- Is critical-path logic derivable from the right truth?

## Boundaries
- What belongs in domain core?
- What belongs in application workflow?
- What belongs in policy / authorization?
- What belongs in integrations?
- What belongs in UI state?
- What belongs in runtime/dev tooling?

## Projections
- Is Gantt a projection?
- Is Mermaid a projection?
- Is CRM dashboard data derived?
- Is activity feed derived?
- Are testing summaries derived?

## AI / agent
- Is AI output proposal state until validated?
- Are embeddings / vectors derived?
- Are token scopes and permissions modeled in policy rather than random entities?
- Are agent writes audited and bounded?

## Runtime / operational
- Are watch manager details mixed into project truth?
- Are snapshots/IPFS abstractions kept out of live canonical state?
- Does storage abstraction leak into the domain?
- Does database switching change semantics, or only persistence?

## Testability
- Can core invariants be tested without UI?
- Can relation invariants be tested without full app startup?
- Are projection builders tested separately from canonical writes?
