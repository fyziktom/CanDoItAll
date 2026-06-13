# Runtime Evidence Consistency Verifier Alpha

This package verifies consistency across caller-supplied Process Core descriptor objects. It does not read runtime state by itself.

## Boundary
- Input is an in-memory Core descriptor payload envelope plus already-resolved descriptor objects.
- Output is deterministic diagnostics, audit facts, evidence references, redaction metadata, and `NoMutationPerformed = true`.
- The verifier does not host drivers, query modules, open files, call HTTP, use DI, schedule retries, apply finalizers, repair providers, or mutate process state.

## In-Memory Sample

```csharp
const string descriptorPayload = """{"source":"caller-supplied-runtime-snapshot"}""";

var evidence = new ProcessDriverEvidenceReference(
    ProcessDriverEvidenceReferenceKind.CoreDescriptor,
    "artifact://proof/sample/runtime-evidence.json",
    ProcessDriverEvidencePolicy.ComputeSha256(descriptorPayload),
    ProcessDriverCoreDescriptorFamily.ExecutionEvidence);
var suppliedContent = ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload(
    evidence,
    descriptorPayload);
var request = new RuntimeEvidenceConsistencyVerificationRequest(
    verificationRequest,
    suppliedContent,
    executionEvidence,
    finalizerEvidence,
    retryDiagnostic,
    noProgressDiagnostic,
    providerRepairDiagnostic,
    projectionSourceOrder,
    DateTimeOffset.UtcNow);

var response = new RuntimeEvidenceConsistencyAlphaVerifier().Verify(request);
```

The descriptor objects must already be in memory before the request is built. This package never fetches them from process services, storage, workspace files, or external systems.

## Non-Goals
- No runtime host, registry, selector, provider, DI registration, hosted service, manager command, scheduler hook, workflow hook, or endpoint mapping.
- No command execution, package restore, HTTP, file/directory access, workspace/storage writes, finalizer application, provider repair, retry scheduling, transition application, claim mutation, or process mutation.
