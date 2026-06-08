# Artifact Evidence Verifier Alpha

This package verifies caller-supplied artifact projection, validation, expectation, and record descriptors. It does not read artifact files or write artifacts.

## Boundary
- Input is an in-memory Core descriptor payload envelope plus already-resolved artifact descriptor snapshots.
- Output is deterministic diagnostics, audit facts, evidence references, redaction metadata, and `NoMutationPerformed = true`.
- The verifier detects projection order drift, missing lineage, trust/sensitivity mismatch, and satisfaction inconsistency from supplied descriptors only.

## In-Memory Sample

```csharp
const string artifactPayload = """{"projection":[{"source":"file-write"}],"validation":[{"kind":"deliverable"}]}""";

var evidence = new ProcessDriverEvidenceReference(
    ProcessDriverEvidenceReferenceKind.CoreDescriptor,
    "bundle://proof/sample/artifact-projection.json",
    ProcessDriverEvidencePolicy.ComputeSha256(artifactPayload),
    ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence);
var suppliedContent = ProcessDriverSuppliedEvidenceContentRules.CreateCoreDescriptorPayload(
    evidence,
    artifactPayload);
var request = new ArtifactEvidenceVerificationRequest(
    verificationRequest,
    suppliedContent,
    projectionLineage,
    projectionSourceOrder,
    providerNativeBrowserEvidence,
    validationRequirements,
    expectedArtifacts,
    artifactRecords,
    DateTimeOffset.UtcNow);

var response = new ArtifactEvidenceAlphaVerifier().Verify(request);
```

The caller must supply all descriptor lists in memory. This package never opens artifacts, reads directories, writes files, persists records, or calls browser/provider APIs.

## Non-Goals
- No artifact writes, file/directory reads, workspace/storage writes, provider calls, browser calls, HTTP calls, DI registration, runtime host, registry, selector, manager command, scheduler hook, workflow hook, finalizer/retry behavior, or process mutation.
