# Record-only Artifact Helper

Completed-decision artifacts do not have managed file content in the current path. Do not force them through storage placement.

Add a small helper such as:

```csharp
internal sealed class ProcessArtifactRecordCoordinator
```

or a narrow method near the existing write coordinator that:

- accepts a plan-like record-only request,
- builds `ProcessArtifactRecordRequest`,
- invokes `RecordArtifactAsync`,
- returns an outcome with external reference key and expectation id.

Keep decision artifact semantics unchanged.
