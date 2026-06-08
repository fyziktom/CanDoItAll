# Roadmap To Stable Process Core With Domain Drivers

## Milestone 1: Stable deterministic Process Core
Status: substantially complete.
- Route descriptors/rules: done.
- Subprocess lifecycle/mapping rules: done.
- Artifact snapshots/matching/satisfaction descriptors: done.
- Execution/finalizer/retry/projection/validation descriptors: done.
- Remaining: maintain API governance, compatibility docs, consumer allow-lists, and descriptor versioning.

## Milestone 2: Contract-only driver abstractions
Status: complete enough for verification alpha.
- Permission mode/value contracts: done.
- Audit/redaction/evidence/verification contracts: done.
- No production runtime registry/selector: still correctly absent.

## Milestone 3: First verification-only domain driver
Status: alpha library exists.
- `.NET/Rust transcript verifier`: production package exists, not integrated into runtime.
- Next step: process-module read-only adapter and evidence pipeline without registry/selector/DI/manager command.

## Milestone 4: Process module controlled consumer
Current bundle.
- Add a process-owned read-only verification adapter.
- Add evidence/transcript payload boundaries with hash validation.
- Return diagnostics/audit/no-mutation envelopes.
- Keep every invocation explicit and testable.
- No generic driver runtime.

## Milestone 5: Descriptor-driven verification enrichment
Future follow-up.
- Use Core execution/finalizer/retry/projection descriptors as supplied evidence.
- Add runtime consistency verifier only as read-only evidence inspector.

## Milestone 6: Office and business-analysis read-only drivers
Future after the .NET/Rust lane is stable.
- Office: no Graph calls, no email mutation, no task creation, no document mutation.
- Business-analysis: no CRM/business-record mutation.

## Milestone 7: Generic driver host / registry
Future only after explicit approval.
- Needs audit persistence, capability policy, command/network/file sandbox, timeout model, secret masking, manager approval, and runtime ownership.

## Milestone 8: Execution-capable drivers
Much later.
- Requires sandbox execution, command allowlists, output hashing, audit persistence, secret masking, timeout, cancellation, artifact policy, and side-effect ownership.
