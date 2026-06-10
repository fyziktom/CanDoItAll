# Requirement Traceability

| Requirement | Source | Subbundle | Proof type |
| --- | --- | --- | --- |
| REQ-001 | User complaint about proof-heavy bundles | SB01/SB08 | `git diff --numstat`, ratio guard test |
| REQ-002 | Need representative templates including multi-team | SB02 | catalog inventory + exact template tests |
| REQ-003 | Need process execution for .NET/Blazor | SB03 | automated dispatch/finalizer integration |
| REQ-004 | Need multi-team development process confidence | SB04 | software-delivery/multi-team automation run |
| REQ-005 | Need non-software process confidence | SB05 | business-analysis automation run |
| REQ-006 | Need runtime-host usefulness tied to real process runs | SB06 | manager readback on real run/step |
| REQ-007 | Need scheduler/workflow job readiness | SB07 | persisted job lifecycle + read-only runner |
| REQ-008 | Need final confidence and next gate | SB08 | release matrix + red-team |
