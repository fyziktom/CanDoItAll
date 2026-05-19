# Cognitive Memory Realistic Validation Architecture Hardening

This follow-up bundle turns the realistic validation findings from `cognitive-memory-cluster-search-realistic-validation` into implementation-ready architecture work.

## Profile

- `initiative`

## Mission

Make Cognitive Memory validation repeatable, policy-correct, source-truth complete, and operable for long-running PostgreSQL and Qdrant validation cycles.

## Source Findings

- Clean PostgreSQL validation succeeded and transferred 13 projects, 263 project objects, 211 links, 263 node bindings, and 2 view-state rows.
- Project-structure ingestion created 13 manifests, 750 source items, and 750 evidence anchors.
- Default consolidation scanned 0 project-structure items because restricted source truth was excluded.
- Restricted consolidation created 80 candidates but hit the candidate budget before all source items were evaluated.
- Quality planning and dreaming required an explicit UI control for restricted source truth.
- Dream aggregate candidates were source-mapped but too generic after restricted redaction and were rejected.
- Probe sessions accepted restricted policy but probe turns reconstructed Project-only policy.
- Qdrant projection worked after explicit projection options, but probe recall did not pass vector projection options and returned `vector:projection-options-missing`.
- Static asset hosting failed in the no-build/production-like startup path until the app ran in Development from the web project directory.
- Database transfer handles project/workbench truth but not external file payload transfer as a first-class source-truth package.

## Outcome Contract

- Validation environments are reproducible from one runbook/API flow.
- Probe, recall, consolidation, cluster planning, and dreaming preserve explicit policy context and projection options.
- Source-truth transfer includes project structures and external file/data manifests with hashes and exclusions.
- Dream outputs are source-detail-aware enough to approve useful aggregate memories and reject noise predictably.
- Long-running validation can continue across multiple cycles with cursoring, budgets, metrics, and approval checkpoints.

## Recommended Execution Order

1. `subbundles/01-validation-host-and-static-assets`
2. `subbundles/02-clean-environment-orchestration`
3. `subbundles/03-source-truth-transfer-completeness`
4. `subbundles/04-policy-preserving-operations`
5. `subbundles/05-dream-aggregate-quality`
6. `subbundles/06-probe-and-recall-loop`
7. `subbundles/07-qdrant-projection-operability`
8. `subbundles/08-long-run-validation-orchestration`

## Closure Evidence Required

- Focused unit/component/integration tests for every fixed policy and projection path.
- API proof for clean PostgreSQL profile creation, transfer, ingestion, consolidation, dreaming, probe, recall, and Qdrant projection.
- Browser proof for large-screen Cognitive Memory quality/cluster/probe operations.
- A completed long-run validation workbook with operation IDs, counts, approval decisions, recall traces, probe feedback, and rejected-output reasons.
