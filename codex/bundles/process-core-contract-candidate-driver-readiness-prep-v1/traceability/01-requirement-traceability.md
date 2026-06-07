# Requirement Traceability

| Requirement | Raw input | Owning subbundles | Planned proof |
| --- | --- | --- | --- |
| REQ-001 Preserve all existing runtime behavior. | Preserve original functionality. | SB001-SB033 | Build, full unit tests, focused process integration tests, critical parity manifests. |
| REQ-002 Do not create Process Core in this bundle. | Do not rush `Process Core`. | SB003, SB030, SB033 | No-Core source scan and final readiness matrix. |
| REQ-003 Do not add production process driver APIs. | Keep future driver work as preparation unless APIs are clearly ready. | SB003, SB030, SB033 | No-driver source scan and docs-only driver readiness proof. |
| REQ-004 Burn down route source-payload and adapter dependencies. | Cover route isolation areas. | SB004-SB006 | Adapter confinement source assertions and focused process tests. |
| REQ-005 Remove dispatcher aliases from finalizer boundary where safe. | Cover finalizer isolation areas. | SB007-SB009 | Finalizer DTO parity tests and source assertions. |
| REQ-006 Split hydration collaborators. | Cover hydration isolation areas. | SB010-SB012 | Hydration parity tests, side-effect ownership assertions, critical proof manifest. |
| REQ-007 Split pre-execution/materialization pure decisions from side effects. | Cover pre-execution and materialization isolation areas. | SB013-SB015 | Start-transition and materialization parity tests. |
| REQ-008 Split subprocess lifecycle orchestration from projection persistence. | Cover subprocess projection areas. | SB016-SB018 | Subprocess lifecycle/projection parity tests and artifact lineage assertions. |
| REQ-009 Slim direct-agent execution and route outcome DTOs. | Cover direct-agent execution isolation. | SB019-SB021 | Direct-agent, retry, provider, and finalizer input parity tests. |
| REQ-010 Align artifact projection and validation DTOs without public Core contracts. | Cover artifact projection and validation convergence. | SB022-SB024 | Projection/validation DTO parity tests and source scans. |
| REQ-011 Move low-risk pure wrappers into tested rule families. | Cover static wrapper burn-down. | SB025-SB027 | Rule parity tests and Core-candidate inventory. |
| REQ-012 Prepare driver-readiness documentation only. | Prepare drivers safely. | SB028-SB030 | Driver lane and safety docs plus no-production-driver scan. |
| REQ-013 Produce final Core readiness decision with evidence. | Do not rush Process Core; fewer broader subbundles. | SB031-SB033 | Scorecard, broad smoke matrix, final red-team closure. |
| REQ-014 Skip browser proof for runtime-only work unless UI changes. | No small/medium/mobile/browser proof. | SB001-SB033 | UI/media diff scan and N/A browser analytics rows. |
