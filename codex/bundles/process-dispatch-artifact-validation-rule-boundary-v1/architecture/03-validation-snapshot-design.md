# Validation Snapshot Design

Create only minimal snapshots that reduce dispatcher nested type coupling. Suggested process-module-local records:

```csharp
internal sealed record ProcessArtifactValidationExpectation(...);
internal sealed record ProcessArtifactValidationCandidate(...);
internal sealed record ProcessArtifactTextContentSnapshot(...);
internal sealed record ProcessArtifactValidationMatchContext(...);
```

Rules:

- snapshots must not reference EF entities;
- snapshots must not reference MAF runtime types;
- snapshots may reference process enums already owned by the Processes module;
- do not move snapshots to `CanDoItAll.Processes.Contracts` unless proven neutral and needed by another module;
- conversion from `DispatchArtifactExpectation` should remain dispatcher-local at first.
