# Cognitive Memory Quality Foundation Audit

Date: 2026-05-19

Scope:
- `src/Modules/CanDoItAll.Modules.CognitiveMemory/Foundation`
- `src/Modules/CanDoItAll.Modules.CognitiveMemory/Consolidation`
- `src/Modules/CanDoItAll.Modules.CognitiveMemory/Recall`
- `src/Modules/CanDoItAll.Modules.CognitiveMemory/ReviewUi`
- `src/Modules/CanDoItAll.Modules.CognitiveMemory/Advanced`

Findings:
- Incremental consolidation was source-item driven and did not have a separate substrate for multi-key memory clusters. Candidate creation operated on one source item at a time, which made cross-source duplicate, contradiction, temporal, and access/risk grouping unobservable before synthesis.
- Dream-like modes existed in the consolidation contracts, but the implementation path was shared with incremental processing. There was no explicit dream run record, cluster agenda, dream-specific metrics, or guard preventing incremental profiles from pretending to be a deep consolidation pass.
- Generated consolidation candidates had source item and evidence anchor fields, but aggregate claims did not have a first-class claim-to-source-map table. That made sentence-level provenance difficult to validate after synthesis.
- Review routing existed for ordinary consolidation candidates, but there was no validation gate for aggregate/dream candidates that could independently reject or route weak, contradictory, restricted, stale, or source-less synthesis to review.
- Recall scoring could mark a candidate as `SideContext`, but focus selection promoted every non-inhibited candidate to `Selected`. That made source-insufficient review-worthy memory eligible for normal context injection.
- Recall context packs exposed detailed source references with the normal response path. There was no compact synthesized brief with reference lookup deferred until explicitly requested.
- Existing metrics covered incremental consolidation counters. They did not include clusters considered, cluster members read, aggregate claim/source-map counts, validation outcomes, evidence coverage, or shallow-run diagnostics.

Implemented direction:
- Add typed quality diagnostics, cluster planning, explicit dream run, aggregate provenance, validation, aggregate application, recall synthesis, and reference resolver contracts.
- Persist clusters, dream runs, run-cluster selections, aggregate candidates, aggregate claims, aggregate claim source maps, validation records, synthesized recalls, synthesized statements, and synthesized statement source maps.
- Keep dream consolidation separate from `IncrementalRecent`; explicit dream requests reject the incremental mode.
- Preserve review-worthy recall candidates as `SideContext` during focus selection.
- Keep references out of synthesized briefs by default and resolve them through a typed on-demand resolver.

Validation focus:
- Targeted unit tests cover diagnostic warnings, all required cluster key families, dream-run metrics, review routing, aggregate application provenance, recall synthesis/reference lookup, and `SideContext` preservation.
- Integration tests verify EF model registration and typed enum persistence for the new quality tables.
