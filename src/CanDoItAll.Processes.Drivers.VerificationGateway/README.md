# Process Driver Verification Gateway

This package is the explicit v1.x entry point for known read-only process driver lanes. It composes alpha verifiers directly and exposes typed methods per lane.

## v1 Public API Snapshot

- Contract line: `v1.x verification-only alpha`.
- Driver abstraction contract source: `ProcessDriverContractVersion.Current` remains `1.10.0`.
- Public type count: `4`.
- Surface hash: `69fd070de1004e6b01f71ae2251d1d3f63b7b2f306d4b165cf3329822f6ad62c`.

Public types:
- `CanDoItAll.Processes.Drivers.VerificationGateway.ProcessDriverVerificationBatchAggregationRequest`
- `CanDoItAll.Processes.Drivers.VerificationGateway.ProcessDriverVerificationBatchRequest`
- `CanDoItAll.Processes.Drivers.VerificationGateway.ProcessDriverVerificationBatchResponse`
- `CanDoItAll.Processes.Drivers.VerificationGateway.ProcessDriverVerificationGateway`

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

## Source-Backed Batch Sample

The request objects in this sample must already be built from caller-supplied in-memory payloads. The gateway sample only shows the typed batching shape.

```csharp
var gateway = ProcessDriverVerificationGateway.CreateDefault();
var request = new ProcessDriverVerificationBatchRequest(
    transcriptRequests: [transcriptRequest],
    runtimeEvidenceRequests: [runtimeEvidenceRequest],
    artifactEvidenceRequests: [artifactRequest],
    officeEvidenceRequests: [officeRequest],
    businessAnalysisRequests: [businessRequest],
    aggregation: new ProcessDriverVerificationBatchAggregationRequest(
        DateTimeOffset.UtcNow,
        "process-readonly-verification"));

var response = gateway.VerifyBatch(request);
```

Use `response.AllResponses` and `response.Aggregate` as diagnostic evidence only. They do not approve runtime execution, persistence, or process mutation.

## Batch Migration Guard

`VerifyBatch` is an additive v1.x convenience over the typed lane methods. It accepts only `ProcessDriverVerificationBatchRequest`, which carries typed request lists for transcript, runtime evidence, artifact evidence, Office evidence, and business analysis. It does not replace the lane-specific methods and does not introduce `Verify(object)`, lane-name dispatch, dynamic lookup, or driver discovery.

`ProcessDriverVerificationBatchAggregationRequest` aggregates already-produced verification responses through the existing observation aggregator. It must not register services, resolve runtime drivers, persist observations, schedule work, trigger manager commands, call connectors, read files, write workspace or storage state, or mutate process state.

`ProcessDriverVerificationBatchResponse.AllResponses` is a read-only concatenation of the typed response lists. Consumers that migrate from direct alpha verifier calls should keep request construction unchanged and treat batch aggregation as diagnostic evidence only.

## Readiness Matrix

| Lane | Input source | Side effects | Status |
| --- | --- | --- | --- |
| Transcript | Caller-supplied transcript text | None | Verification-only alpha |
| Runtime evidence | Caller-supplied Core descriptors | None | Verification-only alpha |
| Artifact evidence | Caller-supplied Core artifact descriptors | None | Verification-only alpha |
| Office evidence | Caller-supplied item metadata and text | None | Verification-only alpha |
| Business analysis | Caller-supplied deliverable/evidence text | None | Verification-only alpha |
| Observation aggregation | Already-produced verification responses | None | Read-only alpha |

## Verification-Pack Manifest Contract

The verification-pack manifest is a review artifact for packaging and compatibility only. It is not a runtime descriptor and must not be loaded by production code for registration or discovery.

Required manifest fields:
- `packId`: stable package identifier.
- `contractVersion`: exact `ProcessDriverContractVersion.Current` value.
- `lanes`: explicit `ProcessDriverVerificationGatewayLane` values with required `ProcessDriverCapabilityScopeKind` and `ProcessDriverPermissionMode.VerificationOnly`.
- `artifacts`: source package, README, test transcript, and source-scan references.
- `noRuntimeRegistration`: must be `true`.
- `noSelfDiscovery`: must be `true`.
- `noExecutionCapableDrivers`: must be `true`.

The manifest must not contain type names used for reflection, assembly scanning, dependency-injection registration, scheduler or workflow hooks, manager commands, workspace or storage writes, external calls, or process mutation. Consumers must keep using typed gateway methods and explicit lane descriptors.

## Non-Goals
- No runtime host, dynamic registry, selector, dependency-injection registration, manager command, scheduler hook, workflow hook, shell execution, connector call, workspace/storage write, persistence, transition/finalizer/retry behavior, or process mutation.
