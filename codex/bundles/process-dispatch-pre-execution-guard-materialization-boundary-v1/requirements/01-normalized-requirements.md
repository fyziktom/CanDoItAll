# Normalized Requirements

| ID | Requirement | Owning Subbundles |
| --- | --- | --- |
| RQ-001 | Preserve current candidate factory/cooperation behavior from previous bundle. | SB01, SB04, SB16, SB19 |
| RQ-002 | Inventory pre-execution guard and upstream materialization code before movement. | SB02 |
| RQ-003 | Add architecture guardrails before production movement. | SB04 |
| RQ-004 | Extract database requirement block decision and transition request shape into local helpers. | SB05-SB06 |
| RQ-005 | Extract upstream artifact gap facts and target selection into local helpers. | SB07-SB08 |
| RQ-006 | Extract materialization fingerprint and duplicate-check semantics. | SB09 |
| RQ-007 | Extract downstream block transition request builder and block reason/directive builders. | SB10 |
| RQ-008 | Extract journal recording/dedup into explicit side-effect coordinator. | SB11 |
| RQ-009 | Extract upstream rerun request builder while preserving directive text. | SB12 |
| RQ-010 | Migrate `TryRequestMissingUpstreamArtifactMaterializationAsync` to use the helpers. | SB13-SB14 |
| RQ-011 | Add a pre-execution route handler facade only after individual helper parity is proven. | SB15 |
| RQ-012 | Run focused runtime smoke, full build, and source scans. | SB16, SB19 |
| RQ-013 | Keep driver readiness documentation-only. | SB17 |
| RQ-014 | Reduce long-file risk without broad cleanup. | SB18 |
| RQ-015 | Final red-team and next cutline. | SB20 |
