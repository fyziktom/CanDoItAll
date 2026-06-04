# Write Coordinator Expansion

## Current Coordinator

Current coordinator responsibilities:

- place bytes through `IStoragePlacementService`
- build `ProcessArtifactRecordRequest` from `ProcessArtifactProjectionPlan`
- invoke `RecordArtifactAsync`
- return managed storage path on success

## Required Improvements

Add a structured result, for example:

```csharp
internal sealed record ProcessArtifactProjectionWriteOutcome(
    string ManagedStoragePath,
    string ExternalReferenceKey,
    Guid? ArtifactExpectationId,
    ProcessArtifactKind ArtifactKind,
    string Title);
```

Keep source semantics outside the coordinator. The coordinator must not decide:

- which source path to use
- which expectation matched
- whether an artifact is duplicate
- whether content should be generated
- whether a candidate should be marked as satisfied

## Failure Semantics

- Process mock required artifacts currently throw when declared artifacts cannot be read or recorded. Preserve this behavior.
- Best-effort projection paths currently log warnings and continue. Preserve this behavior.
- The coordinator can return `Result<ProcessArtifactProjectionWriteOutcome>`, but caller decides whether failure is hard or soft.
