# .NET/Rust Transcript Verifier Alpha

This package is the first verification-only alpha for supplied .NET and Rust transcripts.

## Boundary
- Reads caller-supplied transcript text and evidence references.
- Returns deterministic diagnostics, redaction metadata, audit facts, normalized evidence references, and no-mutation proof.
- References only `CanDoItAll.Processes.Drivers.Abstractions`.

## Explicit Non-Goals
- No command execution, package restore, workspace writes, storage writes, process mutation, Graph or Office calls, runtime registration, selector, host, or manager command.
- No generic process-driver runtime. The process module may consume this verifier only through the narrow read-only adapter that supplies already-resolved transcript content and evidence references.

## API Sample

```csharp
var verifier = new TranscriptVerificationAlphaVerifier();
var response = verifier.Verify(request);
```

The response is accepted only when the permission mode, capability scope, operations, transcript hash, and evidence hashes satisfy the read-only verification contract.

## Process Module Adapter
- `CanDoItAll.Modules.Processes` owns `ProcessTranscriptVerificationReadOnlyAdapter`.
- The adapter accepts supplied transcript text and approved evidence references; it does not open files, call external systems, mutate process state, or register a runtime driver.
- Hash mismatch, unsupported lane, untrusted evidence URI, and mutation attempts return denied observations with `NoMutationPerformed = true`.
- The verifier package still has no dependency on `CanDoItAll.Modules.Processes`, infrastructure, storage, workspace, Graph, Office, or process runtime services.

## Roadmap
- Phase 1: .NET/Rust Transcript Verifier over existing build, test, and proof transcripts.
- Phase 2: Runtime evidence consistency review over immutable Core descriptors.
- Phase 3: Business-analysis evidence review over generated deliverables.
- Phase 4: Office evidence review over already ingested or exported artifacts.
- Phase 5: Execution-capable software driver only after sandbox, timeout, output hashing, secret masking, filesystem policy, network policy, and runtime ownership are approved.
