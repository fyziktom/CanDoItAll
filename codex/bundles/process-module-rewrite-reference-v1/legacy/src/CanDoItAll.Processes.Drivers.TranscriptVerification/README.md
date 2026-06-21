# .NET/Rust Transcript Verifier Alpha

This package verifies caller-supplied .NET and Rust transcript text. It is verification-only and references only `CanDoItAll.Processes.Drivers.Abstractions`.

## Boundary
- Input is in-memory transcript text plus typed evidence references.
- Output is deterministic diagnostics, redaction metadata, audit facts, normalized evidence references, and `NoMutationPerformed = true`.
- The verifier does not execute commands, restore packages, open files, call HTTP, use DI, register a runtime host, dispatch manager commands, schedule work, or mutate process/workspace/storage state.

## In-Memory Sample

```csharp
const string transcriptText = "Build succeeded.";

var transcriptReference = new ProcessDriverTranscriptReference(
    "artifact://proof/sample/dotnet-transcript.txt",
    ProcessDriverEvidencePolicy.ComputeSha256(transcriptText),
    ProcessDriverTranscriptLanguage.DotNet,
    "dotnet",
    "net10.0");
var transcriptEvidence = ProcessDriverEvidencePolicy.CreateTranscriptEvidenceReference(
    transcriptReference,
    transcriptText);
var suppliedContent = ProcessDriverSuppliedEvidenceContentRules.CreateTranscriptText(
    transcriptEvidence,
    transcriptText);
var request = new TranscriptVerificationAlphaRequest(
    verificationRequest,
    transcriptReference,
    suppliedContent,
    transcriptText,
    DateTimeOffset.UtcNow);

var response = new TranscriptVerificationAlphaVerifier().Verify(request);
```

The caller is responsible for constructing `verificationRequest` with `VerificationOnly`, the `DotNetRustTranscriptVerification` scope, read-only operations, and the same evidence reference. Do not load transcript text inside the verifier.

## Non-Goals
- No generic process-driver runtime, registry, selector, provider, DI registration, manager command, scheduler hook, workflow hook, or endpoint mapping.
- No shell execution, package restore, file/directory reads, workspace writes, storage writes, external calls, Office/Graph calls, CRM calls, transition/finalizer/retry behavior, or process mutation.
