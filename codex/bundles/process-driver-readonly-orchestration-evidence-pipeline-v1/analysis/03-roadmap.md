# Roadmap Toward Stable Process Core With Domain Drivers

## Already complete enough
- Deterministic Process Core route/subprocess/artifact/execution/finalizer/diagnostic descriptors.
- Contract-only driver abstractions.
- Read-only alpha driver packages for transcript, runtime evidence, artifact evidence, Office evidence, business analysis, and observation aggregation.
- Explicit gateway over current lanes.
- Process module read-only adapters for domain lanes.
- Full unit baseline is now clean according to latest proof.

## This bundle should complete next
- Consolidate gateway into an explicit typed batch API.
- Split process adapters and make their responsibilities narrow.
- Add process read-only orchestration over supplied payloads.
- Unify evidence/hash/redaction/no-mutation proof across all lanes.
- Reduce direct process-module coupling to individual drivers where safe.
- Keep runtime host unapproved.

## Later bundles
- Controlled read-only runtime integration proposal.
- Audit persistence design and implementation, if approved.
- Manager UI/tool preview for read-only verification results, if approved.
- Scheduler/workflow integration only after a separate runtime-host approval gate.
- Execution-capable drivers only after sandbox, allowlist, timeout, output hashing, audit, secret masking, and lifecycle ownership are implemented and tested.
