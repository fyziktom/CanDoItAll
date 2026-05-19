# Domain Model

## Durable Memory Class Diagram

```mermaid
classDiagram
    class CognitiveMemorySourceManifestRecord {
        Guid Id
        Guid? ProjectId
        string SourceSystem
        string SourceScopeKey
        string SourceSnapshotId
    }

    class CognitiveMemorySourceItemRecord {
        Guid Id
        Guid SourceManifestId
        Guid? ProjectId
        string SourceItemKey
        string ContentHash
        string ContentText
    }

    class CognitiveMemoryEvidenceAnchorRecord {
        Guid Id
        Guid SourceItemId
        string Locator
        string QuoteHash
        CognitiveMemoryRedactionState RedactionState
    }

    class CognitiveMemoryRecord {
        Guid Id
        Guid? ProjectId
        CognitiveMemoryRecordKind Kind
        string Title
        string CanonicalText
        CognitiveMemoryValidationState ValidationState
    }

    class CognitiveMemoryClaimRecord {
        Guid Id
        Guid MemoryRecordId
        string ClaimText
        CognitiveMemoryBeliefStateKind CurrentBeliefState
    }

    class CognitiveMemorySourceLinkRecord {
        Guid Id
        Guid MemoryRecordId
        Guid SourceItemId
        CognitiveMemoryEvidenceRole EvidenceRole
    }

    class CognitiveMemoryRecordEvidenceAnchorRecord {
        Guid MemoryRecordId
        Guid EvidenceAnchorId
    }

    class CognitiveMemoryClaimEvidenceLinkRecord {
        Guid ClaimId
        Guid EvidenceAnchorId
        CognitiveMemoryEvidenceDirection Direction
    }

    class CognitiveMemoryReviewItemRecord {
        Guid Id
        CognitiveMemoryReviewStatus Status
        CognitiveMemoryReviewSubjectKind SubjectKind
        Guid SubjectId
    }

    CognitiveMemorySourceManifestRecord "1" --> "*" CognitiveMemorySourceItemRecord
    CognitiveMemorySourceItemRecord "1" --> "*" CognitiveMemoryEvidenceAnchorRecord
    CognitiveMemoryRecord "1" --> "*" CognitiveMemoryClaimRecord
    CognitiveMemoryRecord "1" --> "*" CognitiveMemorySourceLinkRecord
    CognitiveMemorySourceItemRecord "1" --> "*" CognitiveMemorySourceLinkRecord
    CognitiveMemoryRecord "1" --> "*" CognitiveMemoryRecordEvidenceAnchorRecord
    CognitiveMemoryEvidenceAnchorRecord "1" --> "*" CognitiveMemoryRecordEvidenceAnchorRecord
    CognitiveMemoryClaimRecord "1" --> "*" CognitiveMemoryClaimEvidenceLinkRecord
    CognitiveMemoryEvidenceAnchorRecord "1" --> "*" CognitiveMemoryClaimEvidenceLinkRecord
    CognitiveMemoryReviewItemRecord ..> CognitiveMemoryRecord : may review
```

## Service Class Diagram

```mermaid
classDiagram
    class ICognitiveMemorySourceIngestionService {
        IngestAsync(request)
    }

    class ICognitiveMemoryConsolidationEngine {
        RunAsync(request)
    }

    class ICognitiveMemoryConsolidationCandidateApplicator {
        ApplyAsync(dbContext, candidate, payload)
    }

    class ICognitiveMemoryMutationAuthority {
        SubmitAsync(command)
    }

    class ICognitiveMemoryReviewUiService {
        GetSnapshotAsync(query)
        DecideReviewItemAsync(request)
    }

    class ICognitiveMemoryRecallOrchestrator {
        RecallAsync(request)
    }

    class ICognitiveMemoryScoreGeometryDriver {
        EvaluateAsync(request)
    }

    class ICognitiveMemorySignalLedger {
        PublishAsync(request)
        QueryAsync(query)
    }

    class ICognitiveMemoryProjectionAdapter {
        ProjectAsync(request)
        SearchAsync(request)
        DeleteBySourceAsync(request)
    }

    class ICognitiveMemoryProjectionRebuildService {
        RebuildAsync(request)
    }

    class ICognitiveMemoryScheduledAutomationRunner {
        RunAsync(request)
    }

    class CognitiveMemoryAgentContextPackage {
        FromRecallResult(result)
    }

    class CognitiveMemoryAgentContextContributor {
        ContributeAsync(request)
    }

    ICognitiveMemorySourceIngestionService --> CognitiveMemorySourceManifestRecord
    ICognitiveMemorySourceIngestionService --> CognitiveMemorySourceItemRecord
    ICognitiveMemoryConsolidationEngine --> ICognitiveMemoryMutationAuthority
    ICognitiveMemoryConsolidationEngine --> ICognitiveMemoryConsolidationCandidateApplicator
    ICognitiveMemoryConsolidationCandidateApplicator --> CognitiveMemoryRecord
    ICognitiveMemoryConsolidationCandidateApplicator --> CognitiveMemoryClaimRecord
    ICognitiveMemoryReviewUiService --> ICognitiveMemoryConsolidationCandidateApplicator
    ICognitiveMemoryRecallOrchestrator --> ICognitiveMemoryScoreGeometryDriver
    ICognitiveMemoryRecallOrchestrator --> ICognitiveMemorySignalLedger
    ICognitiveMemoryRecallOrchestrator --> ICognitiveMemoryProjectionAdapter
    ICognitiveMemoryProjectionRebuildService --> ICognitiveMemoryProjectionAdapter
    ICognitiveMemoryProjectionRebuildService --> CognitiveMemoryRecord
    ICognitiveMemoryProjectionRebuildService --> CognitiveMemoryClaimRecord
    ICognitiveMemoryScheduledAutomationRunner --> ICognitiveMemorySourceIngestionService
    ICognitiveMemoryScheduledAutomationRunner --> ICognitiveMemoryConsolidationEngine
    CognitiveMemoryAgentContextContributor --> ICognitiveMemoryRecallOrchestrator
    CognitiveMemoryAgentContextContributor --> CognitiveMemoryAgentContextPackage
```

## Important Model Boundaries

- `CognitiveMemorySourceItemRecord` is ingested source material. It is not canonical memory.
- `CognitiveMemoryEvidenceAnchorRecord` points to source-grounded evidence. Canonical records and claims should be traceable to anchors.
- `CognitiveMemoryRecord` is canonical memory, but validation state still matters. Machine-generated and approved records are not equivalent.
- `CognitiveMemoryClaimRecord` gives claim-level granularity so contradictions, belief state, and evidence can be managed below the whole memory-record level.
- `CognitiveMemoryProjectionRecord` describes projection lifecycle state; the projection backend is rebuildable and not the source of truth.
- `CognitiveMemoryMutationCommandRecord` and audit events are the governance surface for truth mutation.

