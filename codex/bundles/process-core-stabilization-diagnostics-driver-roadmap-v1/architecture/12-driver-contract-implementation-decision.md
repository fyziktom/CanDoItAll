# Driver Contract Implementation Decision

## Decision
Production driver-contract implementation: not ready.

## Rationale
The bundle produced useful docs/tests-only driver vocabulary, lane maps, and permission denials. That is not enough for runtime implementation. A production driver contract would need enforceable permission modes, capability scopes, audit facts, lease/state ownership, command allowlists, timeout policy, secret masking, and failure semantics before any runtime dispatch is safe.

## Prerequisites For A Future Production Proposal

| Prerequisite | Required before implementation |
| --- | --- |
| Permission enforcement | Strongly typed modes and capability scopes; absence of a mode is denied. |
| Runtime ownership | Explicit owner for lease, claim, transition, finalizer, artifact write, and retry side effects. |
| Audit model | Caller identity, process/run/step ids, lane, mode, inspected artifact ids, command/tool identity, denial reason, and redacted diagnostics. |
| Command/tool policy | Allowlist id, working directory, timeout, captured output hash, secret masking, and failure behavior. |
| Isolation model | Sandbox and network/file-system boundaries for any execution-capable future lane. |
| Negative tests | Manager-readonly and verification-only modes must prove they cannot mutate process state or write artifacts. |

## Runtime Dispatch Denial
Runtime dispatch remains denied in this bundle. No production helper-driver API, registry, selector, manager command, service registration, shell execution driver, Office API integration, connector/Graph runtime work, or execution-capable helper is approved.

## Follow-Up Shape
A future bundle may prepare a production driver-contract proposal only after the next Core expansion keeps deterministic read models stable and after the permission/audit/sandboxing design is decomposed into failing-first tests. Until then, use the lane maps only as planning artifacts.

