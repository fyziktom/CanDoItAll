# Process Driver Verification Gateway

This package is the explicit v1.x entry point for known read-only process driver lanes. It composes alpha verifiers directly and exposes typed methods per lane.

## Supported Lanes
- DotNet/Rust transcript verification via `VerifyTranscript`.
- Runtime evidence consistency via `VerifyRuntimeEvidence`.
- Artifact evidence consistency via `VerifyArtifactEvidence`.
- Office evidence read checks via `VerifyOfficeEvidence`.
- Business analysis read checks via `VerifyBusinessAnalysis`.
- Observation aggregation over already-produced verification responses via `AggregateObservations`.

## Migration Guidance

Consumers that previously called alpha verifier packages directly can move to `ProcessDriverVerificationGateway.CreateDefault()` when they need one explicit gateway object for multiple lanes. Keep request construction unchanged: every method still requires caller-supplied in-memory evidence payloads and already-resolved descriptors or items.

Use the typed gateway methods only. Do not introduce lane-name lookup, opaque payload dispatch, background execution, or service discovery as part of v1.x migration.

## Readiness Matrix

| Lane | Input source | Side effects | Status |
| --- | --- | --- | --- |
| Transcript | Caller-supplied transcript text | None | Verification-only alpha |
| Runtime evidence | Caller-supplied Core descriptors | None | Verification-only alpha |
| Artifact evidence | Caller-supplied Core artifact descriptors | None | Verification-only alpha |
| Office evidence | Caller-supplied item metadata and text | None | Verification-only alpha |
| Business analysis | Caller-supplied deliverable/evidence text | None | Verification-only alpha |
| Observation aggregation | Already-produced verification responses | None | Read-only alpha |

## Non-Goals
- No runtime host, dynamic registry, selector, dependency-injection registration, manager command, scheduler hook, workflow hook, shell execution, connector call, workspace/storage write, persistence, transition/finalizer/retry behavior, or process mutation.
