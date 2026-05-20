# Normalized Requirements

| ID | Requirement | Priority | Owning Subbundle |
|---|---|---:|---|
| RQ-01 | Create a regression corpus and failing tests that expose broad low-signal clusters, shallow dreams, unsafe curator targeting, weak synthesis, and missing assimilation. | P0 | 01 |
| RQ-02 | Replace single-key cluster promotion with weighted multi-key cluster scoring. Low-signal keys may support a cluster but must not become aggregate-ready alone. | P0 | 02 |
| RQ-03 | Add cluster cohesion, source independence, source diversity, size, contradiction, and promotion eligibility metrics to cluster planning and persisted cluster records/contracts. | P0 | 02 |
| RQ-04 | Make dream runs select only eligible clusters by mode and produce genuinely synthesized aggregate claims with claim-level provenance, truncation warnings, and uncertainty/conflict framing. | P0 | 03 |
| RQ-05 | Harden dream validation to detect overbroad clusters, weak independence, mixed topics, duplicate aggregates, stale curator-corrected inputs, generated loops, and unsupported claims. | P0 | 03 |
| RQ-06 | Apply aggregate memories with calibrated confidence, lineage, dedupe, and revalidation hooks rather than unconditional strong accept and score 1.0. | P1 | 03 |
| RQ-07 | Replace curator substring capture with structured capture extraction that supports explicit UI/API capture kind, target memory ids, target claim ids, target confidence, scope, and Czech/English phrase baselines. | P0 | 04 |
| RQ-08 | Prevent broad curator supersede/refine. Ambiguous professor corrections must create a review/clarification item or remain as a pending professor anchor, not stale all recall-context memories. | P0 | 04 |
| RQ-09 | Introduce professor anchor lifecycle: active high-trust anchor, compared/integrating, assimilated, faded/retired, and rejected/contradicted. | P1 | 05 |
| RQ-10 | Integrate professor anchors with clustering, dream validation, aggregate invalidation/revalidation, and targeted dream scheduling. | P1 | 05 |
| RQ-11 | Make recall synthesis produce concise requester-facing memory briefs with internal references hidden by default and exact references available on demand. | P1 | 06 |
| RQ-12 | Expand references through aggregate provenance so a synthesized statement can show original memories/source items/anchors when requested. | P1 | 06 |
| RQ-13 | Refactor large services into smaller testable components and require clean build/test/component/browser proof. | P1 | 07 |
| RQ-14 | Keep economic governance and memory resource control out of this implementation. | P0 | All |

## Non-Functional Requirements

- Preserve access policy, redaction, source trust, and mutation audit semantics.
- Use deterministic tests first; optional model/provider calls must not be required for CI.
- Keep new service and data model names explicit enough for future economic governance to plug in later without reworking the foundation.
- All new source comments must be in English.
