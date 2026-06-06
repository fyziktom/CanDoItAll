# Normalized Requirements

| Requirement ID | Requirement | Owner |
| --- | --- | --- |
| RQ-001 | Continue only smaller dispatcher isolation steps; do not start Process Core. | All subbundles |
| RQ-002 | Preserve original projection behavior and source-family order. | SB01-SB84 |
| RQ-003 | Split all-facet projection services into smaller module-local implementations. | SB05-SB60 |
| RQ-004 | Remove or reduce `ProcessArtifactProjectionServices` to a tiny factory/shim. | SB49-SB56 |
| RQ-005 | Do not introduce production driver APIs; driver readiness remains documentation-only. | SB61-SB68 |
| RQ-006 | Do not touch UI or generate mobile/small/medium proof. | All subbundles |
| RQ-007 | Add architecture tests for no all-facet implementation and no broad host resurrection. | SB65-SB68 |
| RQ-008 | Keep source coordinators dependent only on the facets they use. | SB17-SB48 |
| RQ-009 | Keep candidate mutation centralized. | SB08, SB44 |
| RQ-010 | Prepare next safe cutline after this bundle. | SB81-SB84 |
