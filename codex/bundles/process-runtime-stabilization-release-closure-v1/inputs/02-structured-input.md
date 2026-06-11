# Structured Input

## Raw Note Matrix
| Raw note | Normalized requirement | Owning subbundle | Planned proof |
| --- | --- | --- | --- |
| Review real code and tests. | REQ-001 | SB01 | Source review, explicit SHA baseline, code-first ratio guard. |
| Determine whether processes already work like before. | REQ-002, REQ-003, REQ-004, REQ-006 | SB02, SB03, SB05 | UI launch-to-completion, representative automation matrix, scheduler/workflow lifecycle proof. |
| If not, identify what the refactor broke or left incomplete. | REQ-005, REQ-007 | SB04, SB06 | Runtime-host readback closure and live smoke classification. |
| Priority is stabilization before further Process Core extraction. | REQ-008 | SB06 | Boundary scans, final release matrix, explicit merge decision. |

## Scope Controls
- Preserve the hard constraints in `bundle://README.md`.
- Treat all six subbundles as critical because later release proof depends on earlier runtime truth.
- Do not resume Process Core extraction until final release closure is green or explicitly blocked.
