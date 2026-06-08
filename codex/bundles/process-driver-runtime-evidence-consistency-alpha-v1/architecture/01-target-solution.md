# Target Solution

## Production surfaces allowed in this bundle
- Refactored internals of `CanDoItAll.Processes.Drivers.TranscriptVerification`.
- Refactored internals of `ProcessTranscriptVerificationReadOnlyAdapter`.
- New verification-only package, proposed name: `CanDoItAll.Processes.Drivers.RuntimeEvidenceVerification`.
- New immutable request/response payloads for supplied Core descriptor evidence, either in the new package or in `CanDoItAll.Processes.Drivers.Abstractions` only when contract stability requires it.
- New process-module read-only adapter for runtime evidence consistency, with no runtime host or DI wiring.
- Unit and focused integration tests that exercise production paths.

## Runtime evidence verifier alpha
The verifier must:
- accept supplied descriptor payloads only,
- validate permission mode, scope, requested operations, evidence hashes, and descriptor integrity,
- detect contradictions across execution/finalizer/retry/provider/projection/validation descriptors,
- return diagnostics, evidence references, audit facts, redaction descriptor, and `NoMutationPerformed = true`,
- reject unsupported operations and unsafe lanes.

## Denied production surfaces
- registry, selector, host, provider, manager command, DI extension, scheduler hook, workflow hook,
- file IO, workspace/storage write, process mutation, claim mutation, transition mutation, finalizer application,
- shell/dotnet/cargo/package restore execution,
- Office/Graph/external connector calls,
- Core references to any driver package.

## Dependency direction
- `CanDoItAll.Processes.Core` -> contracts only, no driver refs.
- `CanDoItAll.Processes.Drivers.Abstractions` -> no project/package refs unless an explicit future decision changes this.
- `CanDoItAll.Processes.Drivers.TranscriptVerification` -> driver abstractions only.
- `CanDoItAll.Processes.Drivers.RuntimeEvidenceVerification` -> driver abstractions + Core only if descriptors are consumed directly; no Modules/Infrastructure/AgentFramework.
- `CanDoItAll.Modules.Processes` may reference driver packages only through explicit read-only adapters and architecture allow-list tests.
