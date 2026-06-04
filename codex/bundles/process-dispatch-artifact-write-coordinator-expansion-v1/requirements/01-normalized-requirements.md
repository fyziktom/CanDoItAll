# Normalized Requirements

| ID | Requirement | Owner subbundles | Proof |
| --- | --- | --- | --- |
| RQ-001 | Preserve current MAF/Tooling product-neutral boundary. | SB01, SB04, SB08, SB12, SB14 | Source scans and provider tests. |
| RQ-002 | Do not introduce Process Core or driver packs. | All | No-core/no-driver scans. |
| RQ-003 | Inventory all projection write paths and side effects before production migration. | SB01-SB02 | Inventory and line-count proof. |
| RQ-004 | Harden `ProcessArtifactProjectionWriteCoordinator` with structured outcome and failure semantics. | SB03 | Unit tests and source assertions. |
| RQ-005 | Migrate process mock writes through the coordinator without changing hard-failure behavior. | SB05 | Focused integration tests. |
| RQ-006 | Migrate workspace-written writes through the coordinator without changing source/path matching. | SB06 | Key/path parity tests. |
| RQ-007 | Migrate existing-managed writes through the coordinator without changing duplicate detection. | SB07 | Artifact projection tests. |
| RQ-008 | Migrate response-text writes through the coordinator without changing text file content/path safety behavior. | SB09 | Response text projection tests. |
| RQ-009 | Migrate provider-native browser writes through the coordinator without collapsing expected/discovered modes. | SB10 | Browser artifact projection tests. |
| RQ-010 | Add record-only helper for completed-decision artifacts. | SB11 | Decision artifact tests. |
| RQ-011 | Preserve candidate external reference and recorded expectation state updates. | SB05-SB12 | Consumer/source scans and tests. |
| RQ-012 | Keep browser proof N/A and prohibit small/medium/mobile proof artifacts. | All | Proof-path scans. |
| RQ-013 | Require refactor gates every few subbundles. | SB04, SB08, SB12, SB14 | Gate manifests and execution report. |
