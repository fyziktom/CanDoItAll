# SB04 IPFS And FTP Browse Drivers

## Status

- `Completed`

Behavioral proof passed 2026-07-12.

## Objective

- Add truthful native browse implementations for IPFS and FTP with testable transports, explicit unsupported behavior, bounded responses, and safe errors.

## Covered Inputs

- N002-N004, N008, N010-N011, N014-N015; R006-R007, R009, R013-R014, R021, R026-R036, R040.

## Prerequisites

- SB02 Completed. May run alongside SB03 only with disjoint shared-file ownership.

## Exact Source References

- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/IpfsStorageDriver.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FtpStorageDriver.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Abstractions/StorageContracts.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Models/StorageModels.cs`
- `bundle://architecture/10-performance-and-scale.md`
- `bundle://analysis/03-dotnet-performance-audit.md`

## Deliverables

- IPFS browse adapter/transport covering CID/DAG and mutable MFS distinctions, bounded mapping, and driver-proven immutable version only.
- FTP narrow transport seam isolating obsolete protocol API and enabling direct fake tests; shallow browse only when entries can be classified reliably.
- Provider capabilities advertise only implemented root/path/browse/search/stat behavior; unsupported paths return typed errors.
- Credentials/endpoints/raw transport errors are masked.
- Pooled/factory-managed HTTP transport, headers-first response handling, lifetime-owned streams, and explicit byte/range/concurrency/time budgets; no per-call `HttpClient` or full-response bridge buffer.
- Opt-in live integration test instructions use environment-supplied endpoints/secrets; fake transport proof remains mandatory.

## Dependency Impact

- SB05, SB06, SB08, and Resources depend on honest remote capabilities/freshness.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical remote provider foundation.

## Implementation Steps

1. Characterize existing read/write/delete transport behavior.
2. Add narrow transport contracts justified by external protocol/test isolation.
3. Implement IPFS CID/DAG/MFS browse mapping and negatives.
4. Implement FTP reliable shallow browse or explicit Unsupported when server facts are insufficient; never guess folders from names.
5. Add instrumented fake-transport suites for paging, inspected entries/requests, connection reuse, streaming-before-buffering, range/length, concurrency, cancellation, malformed responses, partial facts, secrets, and capability mismatch.
6. Run opt-in live smoke only when configured; report Skipped honestly.
7. Refresh dependency/source review.

## C# Architecture Impact

- Provider/transport isolation within Infrastructure; no new broad provider project required unless an external SDK is selected and justified by a repaired Pattern Selection Record.

## Boundary Ownership

- Provider protocol details stay in provider adapters; cache/authorization/FileTools mapping stay out.

## Dependency Direction

- No FileTools/modules. Any new package requires explicit SB04 pattern/dependency/test record.

## Pattern Decision

- PSR-02 Adapter. A one-implementation transport interface is justified by external protocol isolation and fake testing.

## Testability Contract

- Unit behavior runs with fake transport and no network/credentials; live smoke is supplementary.

## Partial Class Policy

- No partial provider/transport.

## Architecture Proof Required

- Transport boundary, capability matrix, test list, dependency/cycle result, no-secret/error source assertions.

## Scope Exceptions

- No full FTP modernization or provider-native recursive/global search unless proven and required by contract.

## Do Not Do

- Do not parse unreliable FTP detail text as authoritative without tests, treat MFS immutable, fall back to FileSystem/default, swallow remote errors as empty pages, construct `HttpClient` per request, or buffer an unbounded response into `byte[]`/`MemoryStream`.

## Acceptance Checklist

- [x] CID/DAG and MFS policies differ correctly.
- [x] FTP classifies only proven facts and rejects unsupported operations.
- [x] Bounds/cursors/cancellation/malformed/partial response tests pass.
- [x] Transport counters prove connection reuse and bounded streamed content/listing work.
- [x] Logs/errors mask secrets and endpoints.
- [x] Capabilities match behavior.

## Proof Required

- Behavioral positive/negative evidence, fake-transport test commands, optional live results, source/dependency assertions, downstream adapter mapping check.
- Shallow-pass trap: a fake returns a bounded page while production constructs a client per request or buffers the full response. Instrumented negatives must observe connection/stream/read counts, and the realistic positive must consume headers/stream/range within budgets and dispose the owned lifetime correctly.

## Browser Validation Logging

- N/A; Resources browser proof belongs to SB15.

## Progression Gate

- SB05 enters when both remote providers either pass their advertised contract or explicitly reject unsupported operations without fallback.

## Reopen Triggers

- Resources or cache work exposing mutable/immutable confusion, false metadata, secret leak, or provider fallback reopens SB04 and affected downstream proof.

## Suggested Agent Prompt

```text
Implement only truthful IPFS/FTP native browsing behind testable protocol seams. Prefer explicit Unsupported to guessed behavior. Prove bounds, capability honesty, mutable/immutable separation, and secret-safe failures before closure.
```
