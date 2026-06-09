# Roadmap To Stable Generic Process Core With Domain Drivers

## Stable Generic Process Core
- Keep Core deterministic, dependency-clean, and driver-free.
- Keep runtime side effects in process modules or infrastructure, never in Core.
- Keep public Core API snapshots and owner classifications strict.
- Treat Core as pure read-model/rule/descriptor layer.

## Domain Driver Layer
- Keep v1.x drivers read-only over supplied payloads and already-produced descriptors.
- Use strongly typed request/response contracts, capability scopes, evidence references, audit facts, redaction descriptors, and no-mutation proof.
- Use explicit lane methods and batch orchestration, not generic registry/selector/runtime host.
- Add domain lanes only when they have denial tests, corpus fixtures, source scans, and no-mutation semantics.

## Current Release Candidate Goal
Create a stable read-only verification pipeline that can be used by future manager-visible projection or reports without invoking runtime behavior.

## Still Blocked
- Production runtime host.
- Driver registry/selector/DI/manager command.
- Scheduler/workflow invocation.
- Execution-capable drivers.
- Any connector or shell execution.
