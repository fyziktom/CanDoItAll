# Normalized Requirements

| ID | Requirement | Owning subbundles |
| --- | --- | --- |
| R-001 | Preserve all current process automation behavior. | All |
| R-002 | Do not create Process Core. | All, gates |
| R-003 | Do not create production driver APIs. | All, gates |
| R-004 | Split the claimed dispatch route flow into module-local route handlers. | SB005-SB088 |
| R-005 | Preserve exact route order from `ProcessDispatchRoutePipeline.StageOrder`. | SB010, SB023, SB042, SB063, SB094 |
| R-006 | Keep side effects visible through named handlers/coordinators. | SB011, SB012, SB026, SB065-SB068, SB073-SB076 |
| R-007 | Reduce route orchestration line count and wrapper-only code. | SB085-SB088, SB103 |
| R-008 | Keep browser validation N/A and avoid UI/mobile proof. | All |
| R-009 | Add documentation-only driver-readiness map. | SB089 |
| R-010 | Provide individual subbundle proof rows; no collapsed execution report rows. | SB093, SB105-SB112 |
