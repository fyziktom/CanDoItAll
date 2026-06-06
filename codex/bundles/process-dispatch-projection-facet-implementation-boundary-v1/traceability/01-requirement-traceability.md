# Requirement Traceability

| Requirement ID | Raw input | Owning subbundles | Planned proof |
| --- | --- | --- | --- |
| RQ-001 | Continue smaller dispatcher isolation steps and do not rush Process Core. | SB01-SB84 | Gate proof, no-core scans, and final closure proof |
| RQ-002 | Preserve original functionality and projection source-family order. | SB01-SB84 | Focused projection tests and source-family order proof |
| RQ-003 | Split all-facet projection services into smaller module-local implementations. | SB05-SB60 | Source assertions, architecture tests, and critical manifests |
| RQ-004 | Remove or reduce `ProcessArtifactProjectionServices` to a tiny factory/shim. | SB49-SB56 | No all-facet implementation proof and file-size review |
| RQ-005 | Keep driver readiness documentation-only; do not introduce production driver APIs. | SB61-SB68 | No-driver scans and driver-readiness documentation review |
| RQ-006 | Do not touch UI or create small/medium/mobile proof. | All subbundles | No UI source scan and browser analytics marked N/A |
| RQ-007 | Add architecture tests for no all-facet implementation and no broad host resurrection. | SB57-SB60 | Focused architecture test transcript |
| RQ-008 | Keep source coordinators dependent only on facets they use. | SB17-SB48 | Coordinator constructor/source assertion proof |
| RQ-009 | Keep candidate mutation centralized. | SB08, SB44 | Candidate-state source assertion and regression tests |
| RQ-010 | Prepare the next safe cutline after this bundle. | SB81-SB84 | Manager cutline and final no-regression summary |
