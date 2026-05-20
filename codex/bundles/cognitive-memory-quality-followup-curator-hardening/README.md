# Cognitive Memory Quality Follow-up: Curator, Clustering, Dreaming, Synthesis Hardening

This bundle is a follow-up execution package for the Cognitive Memory module after the implementation agent claimed completion of the previous quality-foundation work. It is intentionally scoped to the memory-quality foundation only: clustering, dreaming/consolidation, aggregate validation/application, recall synthesis, reference-on-demand, and the new curator/professor learning mode.

It does **not** include economic memory governance, attention markets, memory budgeting, or cognitive resource pricing. Those models must remain out of scope until the memory foundation can cluster, dream, validate, and learn reliably.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed local structural validation`
- Execution status: `Not started by implementation agent`
- Subbundle gate review: `Seeded and ready for execution`
- Final closure gate: `Requires implementation evidence`
- Browser validation analytics: `Planned where UI-visible; N/A for backend-only subbundles`

## Current Review Summary

The current implementation is a useful baseline but still behaves more like a deterministic consolidation scaffold than a mature cognitive memory system. The main issues are:

1. **Clustering is still mostly single-key grouping.** `CognitiveMemoryClusterPlanner` groups by one key family and one key value at a time. Low-signal keys such as project scope, month, access/risk, and source type can produce large unrelated clusters, and tests currently assert that these families appear as primary clusters.
2. **Dreaming is too shallow.** Dream candidates are built by copying/truncating source memory summaries into a bullet list. The implementation does not yet perform deep semantic abstraction, contradiction framing, evidence independence checks, or aggregate lineage management.
3. **Validation gates are necessary but weak.** The validator checks for missing source maps, stale/restricted sources, contradictions, and all-machine-generated support, but it does not detect overbroad clusters, low cohesion, duplicate aggregates, weak source independence, unsupported synthesized claims, or stale aggregate candidates after curator corrections.
4. **Curator/professor mode is implemented as immediate trusted capture, not as an assimilation model.** It stores user turns as trusted source items and applies them directly, but it does not create a temporary high-trust anchor with lifecycle, assimilation state, cluster influence, targeted revalidation, or gradual fading after the knowledge has been internalized.
5. **Curator correction targeting is unsafe.** The current code can treat all memories included in a recall trace as affected by a correction. A correction should target explicit memories/claims or require review when the target is ambiguous.
6. **Recall synthesis is not yet true synthesis.** It groups selected context sections by title and uses the first line of each section. It should produce a concise, user-facing memory brief with only useful information, while preserving exact reference-on-demand provenance.

## Execution Strategy

Execute the subbundles in order. Do not allow downstream dreaming or curator assimilation work to proceed until the clustering and regression corpus gates are strong enough to prove that broad low-signal clusters no longer become aggregate-ready by accident.

## Source Artifacts

- Current code ZIP: `/mnt/data/CanDoItAll-development (2).zip`
- Previous quality-foundation bundle: `/mnt/data/cognitive-memory-quality-foundation-dreaming-synthesis.zip`
- Extracted current repo used for this review: `/mnt/data/review/CanDoItAll-development`
