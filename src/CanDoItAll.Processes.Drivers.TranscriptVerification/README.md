# .NET/Rust Transcript Verifier Alpha

This package is the first verification-only alpha for supplied .NET and Rust transcripts.

## Boundary
- Reads caller-supplied transcript text and evidence references.
- Returns deterministic diagnostics, redaction metadata, audit facts, normalized evidence references, and no-mutation proof.
- References only `CanDoItAll.Processes.Drivers.Abstractions`.

## Explicit Non-Goals
- No command execution, package restore, workspace writes, storage writes, process mutation, Graph or Office calls, runtime registration, selector, host, or manager command.
- No process-module integration. Runtime adoption requires a later sandbox and allowlist bundle.

## API Sample

```csharp
var verifier = new TranscriptVerificationAlphaVerifier();
var response = verifier.Verify(request);
```

The response is accepted only when the permission mode, capability scope, operations, transcript hash, and evidence hashes satisfy the read-only verification contract.

## Roadmap
- Phase 1: .NET/Rust Transcript Verifier over existing build, test, and proof transcripts.
- Phase 2: Runtime evidence consistency review over immutable Core descriptors.
- Phase 3: Business-analysis evidence review over generated deliverables.
- Phase 4: Office evidence review over already ingested or exported artifacts.
- Phase 5: Execution-capable software driver only after sandbox, timeout, output hashing, secret masking, filesystem policy, network policy, and runtime ownership are approved.
