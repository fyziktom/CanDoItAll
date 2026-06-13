# Business Analysis Verifier Alpha

This package verifies caller-supplied business-analysis deliverable and supporting-evidence text. It does not call CRM, HR, workflow, or storage services.

## Boundary
- Input is an in-memory business-analysis payload envelope plus supplied deliverable/evidence items.
- Output is deterministic diagnostics, audit facts, evidence references, redaction metadata, and `NoMutationPerformed = true`.
- The verifier can report missing requirements, unsupported assumptions, contradiction markers, and evidence gaps from supplied text only.

## In-Memory Sample

```csharp
const string businessPayload = """{"items":[{"kind":"deliverable","id":"analysis-1"}]}""";

var evidence = new ProcessDriverEvidenceReference(
    ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact,
    "artifact://proof/sample/business-analysis.json",
    ProcessDriverEvidencePolicy.ComputeSha256(businessPayload),
    coreDescriptorFamily: null);
var suppliedContent = ProcessDriverSuppliedEvidenceContentRules.CreateBusinessAnalysisPayload(
    evidence,
    businessPayload);
var items = new[]
{
    new BusinessAnalysisEvidenceItem(
        BusinessAnalysisEvidenceItemKind.Deliverable,
        "analysis-1",
        "Churn risk summary",
        "Requirement: explain churn risk. Evidence: supplied interview notes support the conclusion.",
        DateTimeOffset.UtcNow)
};
var request = new BusinessAnalysisVerificationRequest(
    verificationRequest,
    suppliedContent,
    items,
    DateTimeOffset.UtcNow);

var response = new BusinessAnalysisAlphaVerifier().Verify(request);
```

The caller must supply deliverable and evidence text in memory. This package never reads CRM records, files, workspace content, or external systems.

## Non-Goals
- No CRM/business-record mutation, task creation, connector calls, file reads, HTTP calls, DI registration, runtime host, registry, selector, manager command, scheduler hook, workflow hook, workspace write, storage write, transition/finalizer/retry behavior, or process mutation.
