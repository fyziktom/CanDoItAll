# Requirement Traceability

## Raw Audit Notes

| Raw note | Summary | Requirement IDs | Owning subbundle | Closure status |
|---|---|---|---|---|
| C1 | Runtime finalizer attachment does not know effective finalizer mode. | R01 | 01 | Closed |
| C2 | Finalizer instructions conflict with structured response format semantics. | R02 | 01 | Closed |
| C3 | Tool policy exception handling is too broad. | R03 | 02 | Closed |
| C4 | Provider registry persists stale structured-output capability. | R04 | 03 | Closed |
| C5 | Provider transport is inferred by display name. | R05 | 03 | Closed |
| C6 | Verification docs claim tests missing from repository. | R06, R07, R12 | 04, 09 | Closed |
| C7 | Repair service should be named and tested as extraction repair, or upgraded to semantic repair. | R08 | 05 | Closed |
| C8 | Process-step outcome validation is not fully context-aware. | R09 | 06 | Closed |
| C9 | Tool approval composition should fail fast for unusable mutation tools. | R10 | 07 | Closed |
| C10 | Workflow usage is checkpoint-store bridging, not full orchestration. | R11 | 08 | Closed |

## Requirement Owners

| Requirement | Owning subbundle | Proof target |
|---|---|---|
| R01 | 01 | Runtime build attaches finalizer capture only for `Required` and `Shadow` modes. |
| R02 | 01 | Required/shadow finalizer instructions require schema-conformant JSON final responses. |
| R03 | 02 | Dedicated policy-block exception is thrown only by policy branches. |
| R04 | 03 | Workspace provider capability persistence uses the central provider feature matrix. |
| R05 | 03 | Provider transport round-trips through explicit metadata/settings before legacy name inference. |
| R06 | 04, 09 | Verification docs name only test classes present in the repository. |
| R07 | 04 | Focused hardening test classes exist and are discoverable. |
| R08 | 05 | Default repair behavior is documented and tested as conservative JSON extraction. |
| R09 | 06 | Process-context outcome validation is explicit and tested. |
| R10 | 07 | Unusable mutation tools fail or are omitted before model exposure. |
| R11 | 08 | Documentation separates checkpoint bridging from full workflow orchestration. |
| R12 | 09 | Exact build/test commands and results are recorded truthfully. |
