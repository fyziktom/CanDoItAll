# Normalized Requirements

| ID | Requirement | Owner |
| --- | --- | --- |
| REQ-001 | Preserve all existing runtime behavior. | All subbundles |
| REQ-002 | Do not create Process Core in this bundle. | SB003, SB030, SB033 |
| REQ-003 | Do not add production process driver APIs. | SB003, SB030, SB033 |
| REQ-004 | Burn down remaining route source-payload and adapter dependencies. | SB004-SB006 |
| REQ-005 | Remove dispatcher aliases from finalizer application boundary where safe. | SB007-SB009 |
| REQ-006 | Split hydration into explicit query, assembly, binding, recovery, and cooperation collaborators. | SB010-SB012 |
| REQ-007 | Split pre-execution/materialization pure decisions from application side effects. | SB013-SB015 |
| REQ-008 | Split subprocess lifecycle orchestration from projection persistence. | SB016-SB018 |
| REQ-009 | Slim direct-agent execution and route execution outcome DTOs. | SB019-SB021 |
| REQ-010 | Align artifact projection and validation DTOs without creating public Core contracts. | SB022-SB024 |
| REQ-011 | Move only low-risk pure wrappers into tested rule families. | SB025-SB027 |
| REQ-012 | Prepare driver-readiness documentation only. | SB028-SB030 |
| REQ-013 | Produce a final Core readiness decision with evidence. | SB031-SB033 |
| REQ-014 | Do not perform small/medium/mobile/browser proof for runtime-only work. | All subbundles |
