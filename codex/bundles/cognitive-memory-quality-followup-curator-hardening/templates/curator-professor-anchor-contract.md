# Curator Professor Anchor Contract Template

Use this as a conceptual contract when implementing professor anchors.

```csharp
public sealed record CognitiveMemoryProfessorAnchorSnapshot(
    Guid ProfessorAnchorId,
    Guid ProjectId,
    Guid CuratorSessionId,
    Guid CuratorTurnId,
    Guid SourceItemId,
    Guid EvidenceAnchorId,
    string AssertionText,
    string NormalizedClaimText,
    IReadOnlyList<Guid> TargetMemoryRecordIds,
    IReadOnlyList<Guid> TargetClaimIds,
    string ScopeKey,
    CognitiveMemoryCuratorCaptureKind CaptureKind,
    double TargetConfidence,
    CognitiveMemoryProfessorAssimilationState AssimilationState,
    double AssimilationScore,
    IReadOnlyList<Guid> DerivedMemoryRecordIds,
    IReadOnlyList<Guid> DerivedAggregateCandidateIds,
    DateTimeOffset LastComparedAtUtc,
    DateTimeOffset? FadeEligibleAtUtc);
```

The implementation may use existing entity conventions and different names, but it must preserve the lifecycle semantics.
