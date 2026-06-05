# Requirements

| Id | Requirement | Owning Subbundles | Proof |
| --- | --- | --- | --- |
| RQ-001 | Preserve all current process dispatch and artifact validation behavior. | SB01-SB36 | Focused tests, source assertions, full build |
| RQ-002 | Keep all work module-local under `CanDoItAll.Modules.Processes`; do not create Process Core. | SB04, SB08, SB16, SB24, SB32, SB36 | no-core scans |
| RQ-003 | Do not introduce production process driver APIs. | SB04, SB20, SB32, SB36 | no-driver scans |
| RQ-004 | Extract residual artifact classification rules from `ArtifactValidation.cs`. | SB05-SB08 | source scans and tests |
| RQ-005 | Extract provider-native browser output facts and safe path/probe suppression rules. | SB09-SB12 | browser-output parity tests |
| RQ-006 | Extract critical tool failure suppression rules without changing failure behavior. | SB13-SB16 | critical-failure parity tests |
| RQ-007 | Consolidate content type, storage content kind, artifact kind, image/code/project extension classification. | SB17-SB20 | classification parity tests |
| RQ-008 | Preserve external reference key, title, and storage path behavior. | SB21-SB24 | projection/classification tests |
| RQ-009 | Slim wrappers in `ArtifactValidation.cs` after helper movement. | SB25-SB28 | line-count and source scans |
| RQ-010 | Update documentation-only driver-readiness map for residual evidence classification. | SB29-SB32 | no-driver plus map review |
| RQ-011 | No small/medium/mobile proof; browser validation remains N/A unless UI files unexpectedly change. | SB01-SB36 | proof-path scan |
| RQ-012 | Every critical gate must record manifests, semantic invariants, source scans, anti-stub audit, and downstream dependency review. | SB04, SB08, SB12, SB16, SB20, SB24, SB28, SB32, SB36 | manifests |
