# Requirements

| ID | Requirement | Type |
| --- | --- | --- |
| RQ-001 | Continue module-local dispatcher isolation before Process Core. | Architecture |
| RQ-002 | Preserve all existing artifact projection source families and source order. | Behavior |
| RQ-003 | Keep all projection helper/coordinator code under `CanDoItAll.Modules.Processes`. | Architecture |
| RQ-004 | Do not add production process driver APIs or registries. | Constraint |
| RQ-005 | Do not add `CanDoItAll.Processes.Core`. | Constraint |
| RQ-006 | Separate pure projection planning from side-effectful projection coordination. | Architecture |
| RQ-007 | Centralize candidate state update after write outcomes. | Behavior |
| RQ-008 | Migrate each projection source path behind a focused coordinator with tests. | Refactor |
| RQ-009 | Keep completed-decision record-only projection separate from storage-backed writes. | Behavior |
| RQ-010 | Provide focused positive and negative tests per migrated source family. | Validation |
| RQ-011 | Keep browser validation N/A unless UI files are unexpectedly changed. | Validation |
| RQ-012 | No small/medium/mobile/phone/tablet proof artifacts. | Constraint |
| RQ-013 | Add documentation-only driver-readiness map for projection evidence families. | Driver readiness |
| RQ-014 | Run full build and focused regression matrix before final closure. | Validation |
| RQ-015 | Document known unrelated failures separately instead of hiding them. | Validation |
