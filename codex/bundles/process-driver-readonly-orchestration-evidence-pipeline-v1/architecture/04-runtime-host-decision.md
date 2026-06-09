# Runtime Host Decision

## Decision
Runtime host remains not approved.

Current decision: all runtime-host surfaces are `Not approved`.

Contract line: `v1.x verification-only alpha`.

## Why
The system now has useful read-only domain driver packages and an explicit gateway, but production runtime integration requires additional audit persistence, lifecycle ownership, authorization, operational controls, and failure semantics.

## Approved next work
- Explicit batch gateway.
- Process read-only orchestration over supplied payloads.
- More source-backed tests.
- Documentation and release gates.

## Still denied
- Generic runtime host.
- Driver registry/selector/DI.
- Manager command.
- Scheduler/workflow integration.
- Execution-capable drivers.
- Any mutation side effect.

## Runtime Host Approval Matrix

| Surface | Status | Reason |
| --- | --- | --- |
| Runtime host | `Not approved` | No lifecycle owner, runtime authorization model, operational failure semantics, or durable audit persistence exists for invoking drivers outside supplied in-memory evidence. |
| Driver registry | `Not approved` | v1.x uses explicit typed gateway methods only; no dynamic discovery or late-bound driver catalog is approved. |
| Runtime selector | `Not approved` | Lane choice remains compile-time typed through the gateway; no selector may resolve drivers by string, object payload, or runtime state. |
| Dependency injection registration | `Not approved` | Driver packages are directly composed by the explicit gateway; no service registration, startup hook, or container resolution is approved. |
| Manager command | `Not approved` | Manager-visible commands require separate authorization, audit persistence, denial semantics, and operator UX design. |
| Scheduler hook | `Not approved` | Background execution requires ownership, idempotency, retry, cancellation, and failure-reporting rules that are not satisfied in this bundle. |
| Workflow hook | `Not approved` | Workflow-triggered driver invocation would be runtime orchestration, not read-only supplied-evidence verification. |
| Execution-capable drivers | `Not approved` | `ExecutionCapableFuture` remains a denied marker, not a usable permission mode. |
| File/network/storage/workspace mutation | `Not approved` | The current pipeline accepts caller-supplied payloads only and must not read files, call connectors, write storage, write workspace state, or mutate process state. |

## Future Approval Prerequisites

Every prerequisite in this section is `Not satisfied`.

| Prerequisite | Status | Required proof before reconsideration |
| --- | --- | --- |
| Audit persistence | `Not satisfied` | Durable records for request id, caller context, lane, permission mode, requested operation, capability scope, denial reason, decision, timestamp, and redaction outcome. |
| Runtime lifecycle ownership | `Not satisfied` | Named owning module, startup/shutdown boundaries, cancellation policy, retry policy, deployment responsibility, and support path. |
| Authorization and approval | `Not satisfied` | Explicit approval model for who may invoke runtime drivers, how approval is recorded, revoked, expired, audited, and denied. |
| Sandbox and allow-list policy | `Not satisfied` | Process isolation, resource limits, command and connector allow-lists, path allow-lists, and tests proving unknown commands, connectors, paths, lanes, and operations fail predictably. |
| Failure semantics | `Not satisfied` | Deterministic handling for partial failures, retry exhaustion, cancellation, duplicate requests, timeout, and invalid evidence. |
| Compatibility governance | `Not satisfied` | Versioned public API snapshot, migration policy, semantic compatibility tests, and explicit review of `ProcessDriverContractVersion.Current`. |
| Red-team negative proof | `Not satisfied` | Tests proving report-only approval, status-only docs, dynamic dispatch, service registration, manager command, scheduler hook, workflow hook, file/network access, and mutation attempts are rejected. |
