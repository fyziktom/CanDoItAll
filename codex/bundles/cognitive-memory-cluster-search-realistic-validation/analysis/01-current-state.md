# Current State

The prior UI follow-up exposed quality diagnostics, cluster planning, dream aggregation, validation, synthesis, and paging for existing review UI collections. The missing operator affordance is a search-focused cluster tab. Operators can see cluster summaries but cannot search cluster keys or filter cluster readiness/risk without loading or scanning unrelated rows.

The quality cluster persistence model already stores bounded searchable metadata:

- `CognitiveMemoryQualityClusterRecord` stores cluster identity, readiness, access/risk, counts, contradictions, and timestamps.
- `CognitiveMemoryQualityClusterKeyRecord` stores family/key/display text values suitable for search facets.
- `CognitiveMemoryQualityClusterMemberRecord` stores bounded member/evidence linkage and validation/stability state.

The API exposes database profile, ingestion, consolidation, probe, approval, recall, and advanced validation endpoints. The current snapshot API contract is narrower than the Blazor review UI service and may need follow-up work if external clients require cluster search through HTTP.

The project transfer model exists in infrastructure and module handlers, but the exact project-data transfer path must be validated before claiming that a clean database profile can receive all prior projects and structures automatically.
