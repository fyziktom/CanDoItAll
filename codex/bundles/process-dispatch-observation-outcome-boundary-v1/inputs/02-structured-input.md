# Structured Input

## Raw Notes

| Raw note | Normalized requirement | Owning subbundles | Closure proof expectation |
| --- | --- | --- | --- |
| Continue smaller dispatcher isolation steps | RQ-004 through RQ-010 | SB05-SB40 | Focused tests, source assertions, line-count proof |
| Do not rush Process Core | RQ-002 | SB01, SB04, SB42, SB44, SB48 | No-core source scans |
| Preserve original behavior | RQ-001, RQ-004 through RQ-008 | SB05-SB40, SB43-SB44 | Focused positive/negative tests and broad smoke |
| Prepare future drivers without production APIs | RQ-003, RQ-012 | SB41, SB42, SB44, SB48 | Documentation-only map and no-driver scan |
| Split into more phases and force gates | RQ-013 | SB01-SB48 | Gate rows and critical proof manifests |
| Do not use small/medium/mobile proof | RQ-011 | All subbundles | Proof-path scan |

## Scope Boundary

- Production changes are limited to module-local dispatch helpers under `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/`.
- UI, browser proof, Process Core creation, and production driver APIs are out of scope.
