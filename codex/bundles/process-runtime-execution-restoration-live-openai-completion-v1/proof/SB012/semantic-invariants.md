# SB012 Semantic Invariants

## Status
Completed.

## Invariant SB012_INV_001
- Invariant ID: `SB012_INV_001`
- Source raw note: "Review real code, not only bundle report" and "Determine real test outcome."
- Expected behavior: Dispatch after outbox claim executes a typed route, finalizes step/run state, projects required artifacts, persists managed artifacts where applicable, and exposes workflow/artifact readback.
- Disallowed shallow implementation: Counting an outbox drain, calling dispatch directly, or marking a step complete without proving artifact projection and readback.
- Failing-first test: `bundle://proof/SB012/red-team/outbox-only-proof-rejection.txt` rejects outbox-only or call-count-only proof.
- Passing test: Four focused integration tests passed in `bundle://proof/SB012/transcripts/route-finalizer-artifact-e2e-tests.txt`.
- Changed source files: No production source changed in SB012. Current source hashes are captured in `bundle://proof/SB012/manifest.md`.
- Production assertions: `bundle://proof/SB012/transcripts/route-finalizer-artifact-source-assertions.txt` cites dispatch route, claim lifecycle, finalizer, artifact projection, and readback test surfaces.
- Red-team negative case: `bundle://proof/SB012/red-team/outbox-only-proof-rejection.txt`
- Downstream dependency check: SB013-SB018 may start because dispatch now proves executable route/finalizer/artifact behavior, not just durable enqueue.

## Shallow-Pass Trap
A fake Gate D closure could say the outbox worker ran or that a mock provider was invoked. SB012 rejects that by requiring durable dispatch E2E, workflow assignment/detail tests, managed artifact handoff, artifact records, and run-detail readback.

## Semantic Positive Proof
- `bundle://proof/SB012/transcripts/route-finalizer-artifact-e2e-tests.txt`
- `bundle://proof/SB012/transcripts/route-finalizer-artifact-source-assertions.txt`

## Adversarial Negative Proof
- `bundle://proof/SB012/red-team/outbox-only-proof-rejection.txt`

## Anti-Stub Audit
- `bundle://proof/SB012/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Matches are documentation and negative test assertions, not an execution-capable process-driver runtime host, process-driver registry, selector, or production `NotImplemented` path.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Dispatch route result | `ProcessRunAutomationDispatchService.RouteExecution.cs` | Step finalizer and runtime state | Converts route handler result into persisted process transitions | Outbox-only rejection prevents treating dispatch count as route proof |
| Finalized step/run state | Step completion finalizer | Runtime readback and next-step dispatch | Persists completed/waiting/blocked transitions and run closure | Tests inspect final detail state, not just returned method values |
| Artifact records and managed artifacts | Artifact projection coordinators | Run detail, downstream handoff, project output proof | Required outputs become persisted artifact records and readable managed files | Artifact handoff tests reject missing or unreadable required outputs |
