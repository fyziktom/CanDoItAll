# Normalized Requirements

| ID | Requirement | Priority |
|---|---|---|
| R-01 | Produce a source-level implementation audit and baseline quality metrics before changing behavior. | Critical |
| R-02 | Add multi-key clustering support across semantic, source, project, temporal, entity, task, evidence, and relation keys. | Critical |
| R-03 | Persist cluster run and membership data so dream work is explainable and repeatable. | Critical |
| R-04 | Split incremental consolidation from explicit dream consolidation behavior. | Critical |
| R-05 | Implement distinct behavior for important consolidation modes instead of a single per-item loop. | Critical |
| R-06 | Create aggregate memory candidates from clusters, not only from individual source items. | Critical |
| R-07 | Represent aggregate claims with evidence/source provenance at claim or sentence level. | Critical |
| R-08 | Validate generated aggregate claims against source coverage, contradiction pressure, redaction, and temporal/stability state. | Critical |
| R-09 | Route uncertain, contradictory, high-risk, or weakly grounded aggregates to review instead of activation. | Critical |
| R-10 | Fix recall focus selection so SideContext/review-worthy candidates are not promoted to Selected. | Critical |
| R-11 | Add a recall synthesis layer that produces concise consumer-specific briefs from selected memories. | Critical |
| R-12 | Add reference-on-demand APIs/DTOs that can explain every synthesized statement with source memory/source item/evidence anchors. | Critical |
| R-13 | Keep diagnostic scores and raw references out of normal consumer-facing text unless explicitly requested. | High |
| R-14 | Preserve redaction and access policy in dream aggregation, synthesized answers, and reference expansion. | Critical |
| R-15 | Add quality metrics proving dream work depth: clusters considered, members read, claims extracted, aggregates produced, validations run, rejected/approved counts, elapsed timing, and evidence coverage. | Critical |
| R-16 | Build regression corpus/tests for duplicates, contradictions, temporal supersession, multiple projects, and restricted content. | Critical |
| R-17 | Preserve existing review, mutation, projection, and context-pack capabilities unless replaced by a tested safer path. | High |
| R-18 | Do not include economic memory management in this implementation pass. | Critical |
| R-19 | Update docs and execution reports so future bundles no longer rely on pre-P0/P1 assumptions. | High |
