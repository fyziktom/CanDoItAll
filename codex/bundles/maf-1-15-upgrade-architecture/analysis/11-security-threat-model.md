# Migration Security Threat Model

| Threat | Attack/failure mode | Existing/new control | Required proof |
|---|---|---|---|
| Forged approval | Client invents request ID and tool call | MAF binding + application pending store | Unknown ID invokes nothing |
| Tool substitution | Correct ID, different tool name | MAF rebind to recorded call | Original tool or rejection |
| Argument substitution | Correct ID, modified path/command/amount | MAF rebind + fingerprint | Original arguments only |
| Replay | Same approval response submitted twice | MAF one-turn consumption + transactional app state | Exact-once invocation |
| Cross-session approval | Request from session A used in B | Session state + application ownership | No invocation |
| Cross-run/process-step approval | Old request applied to new execution | Run/process/step fingerprint | No invocation |
| Legacy state confusion | 1.13 response-only continuation under 1.15 | Version classifier and reissue | Typed reissue, no silent drop/execute |
| Persistent record tampering | Stored tool/arguments modified | HMAC/authenticated store + MAF native state | Tamper rejected |
| Batch race | One boolean applies to newly added requests | Per-ID decisions and concurrency control | Only selected IDs change |
| Missing request ID | Random ID makes request appear valid | Fail closed; no random fallback | Not approvable |
| Mixed-call auto bypass | Mutation misclassified as non-approval-required | Application wrapper classification and parity feature flag | Mutation still requires approval |
| State scrub corruption | Attachment scrub removes binding state | Narrow scrub + round-trip test | Approval resumes after scrub |
| Cache authority drift | Process cache differs from persistence | Persistent canonical state and compare | Restart works; mismatch rejected |
| Workflow output confusion | Intermediate agent text used as final machine result | Terminal projector and response contract | Terminal output selected |
| Duplicate workflow execution | Run streaming then non-streaming | Architecture prohibits second execution | Invocation counters remain one |
| Message reorder | Tool result separated from call | MAF merge + application merge tests | Adjacency preserved |
| File tool duplication | Harness and custom tools expose same operation | Full discovery and tool inventory | No unexplained duplicate |
| Workspace escape | Model supplies traversal/reparse path | Existing workspace path/scope policy | Escape blocked |
| Rollback downgrade | 1.15 state interpreted unsafely by 1.13 | Backup and bidirectional test | Restore or explicit rejection |
| Sensitive logging | Arguments/session JSON leaked during diagnostics | Redacted structured telemetry | No secrets/raw attachment bytes |
| Live-state pooling | Approval/session/tool leaks across executions | Per-run runtime build and concurrency tests | Isolation |
| Provider custom stack | Binding omitted for one provider | Provider inventory and behavioral probe | Every provider secured |
| Unsupported approval transport | Mutation tool exposed where approval cannot round-trip | Existing filter/fail-closed policy | Tool absent or run rejected |
| Expired bridge permanence | Temporary legacy compatibility becomes normal path | Feature flag, expiry, metrics, removal gate | Zero backlog and bridge off |
