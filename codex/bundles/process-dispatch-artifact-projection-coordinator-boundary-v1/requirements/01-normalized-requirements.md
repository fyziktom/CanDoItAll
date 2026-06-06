# Normalized Requirements

| ID | Requirement | Type | Owning subbundles |
| --- | --- | --- | --- |
| RQ-001 | Continue module-local dispatcher isolation before Process Core. | Architecture | SB01-SB56 |
| RQ-002 | Preserve all existing artifact projection source families and source order. | Behavior | SB09-SB48, SB54 |
| RQ-003 | Keep all projection helper/coordinator code under `CanDoItAll.Modules.Processes`. | Architecture | SB04, SB08, SB52, SB56 |
| RQ-004 | Do not add production process driver APIs or registries. | Constraint | SB04, SB08, SB14, SB20, SB26, SB32, SB38, SB44, SB48, SB52, SB56 |
| RQ-005 | Do not add `CanDoItAll.Processes.Core`. | Constraint | SB04, SB08, SB14, SB20, SB26, SB32, SB38, SB44, SB48, SB52, SB56 |
| RQ-006 | Separate pure projection planning from side-effectful projection coordination. | Architecture | SB05-SB52 |
| RQ-007 | Centralize candidate state update after write outcomes. | Behavior | SB07-SB08, SB52 |
| RQ-008 | Migrate each projection source path behind a focused coordinator with tests. | Refactor | SB09-SB48 |
| RQ-009 | Keep completed-decision record-only projection separate from storage-backed writes. | Behavior | SB45-SB48 |
| RQ-010 | Provide focused positive and negative tests per migrated source family. | Validation | SB13, SB19, SB25, SB31, SB37, SB43, SB54 |
| RQ-011 | Keep browser validation N/A unless UI files are unexpectedly changed. | Validation | all gates |
| RQ-012 | No small/medium/mobile/phone/tablet proof artifacts. | Constraint | all gates |
| RQ-013 | Add documentation-only driver-readiness map for projection evidence families. | Driver readiness | SB53, SB56 |
| RQ-014 | Run full build and focused regression matrix before final closure. | Validation | SB54-SB56 |
| RQ-015 | Document known unrelated failures separately instead of hiding them. | Validation | SB55-SB56 |
