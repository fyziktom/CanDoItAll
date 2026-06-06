# Requirement Traceability

| Requirement | Owning subbundles | Source files | Proof |
| --- | --- | --- | --- |
| REQ-001 | SB004, SB120, SB128 | all changed source | no-Core/no-driver source scan |
| REQ-002 | SB008, SB028, SB080, SB124 | route handlers/factory/tests | route order tests + integration tests |
| REQ-003 | SB009-SB020 | route model files | route model adapter tests |
| REQ-004 | SB019, SB109, SB112 | route facets/handlers/services | forbidden alias scan |
| REQ-005 | SB041-SB108 | route services | no all-facet service scan |
| REQ-006 | SB105-SB108 | route handler factory | source assertion |
| REQ-007 | SB017, SB027, SB052, SB100 | route side-effect matrix | side-effect matrix + tests |
| REQ-008 | SB117-SB120 | driver-readiness docs | no driver API source scan |
| REQ-009 | all | source diff | no UI/mobile proof scan |
| REQ-010 | SB119, SB128 | execution report | no collapsed rows scan |
