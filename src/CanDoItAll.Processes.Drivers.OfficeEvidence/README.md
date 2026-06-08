# Office Evidence Verifier Alpha

This package verifies caller-supplied Office evidence metadata and text. It does not call Office, Graph, Gmail, or any connector.

## Boundary
- Input is an in-memory Office evidence payload envelope plus already-supplied email/document item metadata and text.
- Output is deterministic diagnostics, audit facts, evidence references, redaction metadata, and `NoMutationPerformed = true`.
- The verifier denies category mutation, task creation, document writes, attachment fetches, Graph calls, workspace writes, storage writes, and process mutation.

## In-Memory Sample

```csharp
const string officePayload = """{"items":[{"kind":"email","id":"message-1"}]}""";

var evidence = new ProcessDriverEvidenceReference(
    ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact,
    "bundle://proof/sample/office-evidence.json",
    ProcessDriverEvidencePolicy.ComputeSha256(officePayload),
    coreDescriptorFamily: null);
var suppliedContent = ProcessDriverSuppliedEvidenceContentRules.CreateOfficeEvidencePayload(
    evidence,
    officePayload);
var items = new[]
{
    new OfficeEvidenceItem(
        OfficeEvidenceItemKind.EmailMessage,
        "message-1",
        "Escalation follow-up",
        "manager@example.invalid",
        ["owner@example.invalid"],
        DateTimeOffset.UtcNow,
        "Caller supplied the message body text.")
};
var request = new OfficeEvidenceVerificationRequest(
    verificationRequest,
    suppliedContent,
    items,
    DateTimeOffset.UtcNow);

var response = new OfficeEvidenceAlphaVerifier().Verify(request);
```

The caller must supply all item metadata and text in memory. This package never fetches messages, attachments, documents, or connector data.

## Non-Goals
- No Office/Graph/Gmail connector calls, attachment fetches, task creation, category mutation, document writes, DI registration, runtime host, registry, selector, manager command, scheduler hook, workflow hook, workspace write, storage write, or process mutation.
