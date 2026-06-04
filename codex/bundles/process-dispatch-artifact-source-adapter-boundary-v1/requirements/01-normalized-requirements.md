# Normalized Requirements

| ID | Requirement | Owning subbundles |
| --- | --- | --- |
| RQ-001 | Preserve prior execution snapshot and provider decoupling boundaries. | SB01, SB12 |
| RQ-002 | Inventory all remaining artifact projection source paths and side effects before movement. | SB02 |
| RQ-003 | Introduce local artifact projection snapshots/adapters without Process Core or driver packs. | SB03, SB04 |
| RQ-004 | Remove dispatcher nested-type dependencies from new artifact helpers where practical. | SB03, SB04 |
| RQ-005 | Migrate process mock projection planning through a source adapter with exact key parity. | SB05 |
| RQ-006 | Migrate workspace-written and existing-managed projection planning through adapters. | SB06 |
| RQ-007 | Run a refactor gate proving source adapter parity and no behavior drift. | SB07 |
| RQ-008 | Migrate assistant-response and provider-native browser projection planning through adapters. | SB08 |
| RQ-009 | Introduce a local write coordinator/facade for artifact storage and record operations. | SB09 |
| RQ-010 | Migrate only the execution-artifact write path to the write coordinator first. | SB10 |
| RQ-011 | Prove artifact lineage, duplicate protection, trust status and required-artifact behavior remain stable. | SB07, SB11, SB12 |
| RQ-012 | Keep browser proof N/A or PC/large-screen only; reject small/medium/mobile proof artifacts. | All |
| RQ-013 | Final red-team cutline must identify the next dispatcher isolation target without starting Process Core. | SB12 |
