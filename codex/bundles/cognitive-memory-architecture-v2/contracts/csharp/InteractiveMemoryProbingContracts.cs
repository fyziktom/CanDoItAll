using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CanDoItAll.CognitiveMemory.Abstractions;

public enum MemoryProbeSessionMode
{
    FreeDialogue = 0,
    GuidedExam = 1,
    GapHunting = 2,
    ContradictionHunt = 3,
    ContextSeparationDrill = 4,
    ProcedureDrill = 5,
    SourceAudit = 6,
    LearningValidation = 7,
    RegressionReplay = 8
}

public enum MemoryProbeTurnKind
{
    UserQuestion = 0,
    SystemGeneratedQuestion = 1,
    UserCorrection = 2,
    SourceChallenge = 3,
    WhyDoYouThinkThat = 4,
    FollowUp = 5
}

public enum MemoryProbeFindingKind
{
    Unknown = 0,
    Confirmed = 1,
    PartiallyCorrect = 2,
    Incorrect = 3,
    MissingKnowledge = 4,
    Ambiguous = 5,
    ContradictionSuspected = 6,
    WrongScope = 7,
    TooGeneric = 8,
    Overconfident = 9,
    UnsafeOrRedacted = 10,
    NeedsSourceReview = 11,
    RegressionCandidate = 12
}

public enum MemoryProbeFeedbackAction
{
    Confirm = 0,
    Correct = 1,
    MarkMissing = 2,
    MarkAmbiguous = 3,
    MarkWrongScope = 4,
    RequestSource = 5,
    CreateReviewItem = 6,
    CreateRegressionTest = 7,
    RequestLearningProposal = 8,
    Snooze = 9,
    Ignore = 10
}

public enum MemoryProbeQuestionOrigin
{
    ManualUser = 0,
    EpistemicDrive = 1,
    CoverageMap = 2,
    RecallFailure = 3,
    Contradiction = 4,
    Staleness = 5,
    ContextSeparation = 6,
    RegressionReplay = 7,
    SerendipityWalk = 8
}

public enum MemoryRegressionTestState
{
    Draft = 0,
    Active = 1,
    Passing = 2,
    Failing = 3,
    NeedsReview = 4,
    Retired = 5
}

public sealed record MemoryProbeSessionStartRequest(
    Guid ProjectId,
    MemoryProbeSessionMode Mode,
    string Purpose,
    MemoryAccessContext AccessContext,
    Guid? KnowledgeRegionId,
    IReadOnlyList<Guid> SeedMemoryItemIds,
    IReadOnlyDictionary<string, string> Options);

public sealed record MemoryProbeSessionRecord(
    Guid Id,
    Guid ProjectId,
    MemoryProbeSessionMode Mode,
    string Purpose,
    MemoryAccessContext AccessContext,
    Guid? KnowledgeRegionId,
    string State,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MemoryProbeQuestionRequest(
    Guid ProjectId,
    Guid? SessionId,
    MemoryProbeSessionMode Mode,
    Guid? KnowledgeRegionId,
    IReadOnlyList<Guid> ActiveProjectDirectionIds,
    int Limit,
    double SerendipityBudget,
    MemoryAccessContext AccessContext,
    IReadOnlyDictionary<string, string> Options);

public sealed record MemoryProbeQuestion(
    Guid Id,
    Guid ProjectId,
    Guid? KnowledgeRegionId,
    string Question,
    string Purpose,
    MemoryProbeQuestionOrigin Origin,
    IReadOnlyList<Guid> SuggestedMemoryItemIds,
    IReadOnlyList<KnowledgeGapEvidenceRef> EvidenceRefs,
    IReadOnlyDictionary<string, string> ExpectedAnswerHints,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset CreatedAtUtc);

public sealed record MemoryProbeTurnRequest(
    Guid SessionId,
    string Text,
    MemoryProbeTurnKind TurnKind,
    RecallIntent Intent,
    bool IncludeTrace,
    IReadOnlyDictionary<string, string> Options);

public sealed record MemoryProbeAnswerMetadata(
    ScoreEvaluationTrace AnswerEvaluationTrace,
    ScoreScalarProjection? DisplayConfidence,
    bool ContainsUnverifiedClaims,
    bool ContainsGeneratedSummaryClaims,
    bool HasRequiredSourceRefs,
    bool HasContradictionWarnings,
    bool HasStalenessWarnings);

public sealed record MemoryProbeFinding(
    Guid Id,
    MemoryProbeFindingKind Kind,
    string Summary,
    ScoreEvaluationTrace FindingTrace,
    ScoreScalarProjection? DisplayConfidence,
    IReadOnlyList<Guid> RelatedMemoryItemIds,
    IReadOnlyList<MemorySourceRef> SourceRefs,
    IReadOnlyList<KnowledgeGapEvidenceRef> EvidenceRefs,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MemoryProbeAnswerResult(
    Guid SessionId,
    Guid TurnId,
    string Answer,
    Guid RecallTraceId,
    Guid? ContextPackId,
    MemoryProbeAnswerMetadata Metadata,
    IReadOnlyList<MemoryProbeFinding> Findings,
    IReadOnlyList<MemoryProbeSuggestedAction> SuggestedActions,
    IReadOnlyList<string> Warnings);

public sealed record MemoryProbeSuggestedAction(
    MemoryProbeFeedbackAction Action,
    string Label,
    string Reason,
    IReadOnlyDictionary<string, string> Arguments);

public sealed record MemoryProbeFeedbackRequest(
    Guid SessionId,
    Guid TurnId,
    MemoryProbeFeedbackAction Action,
    string? UserComment,
    string? CorrectedAnswer,
    IReadOnlyList<MemorySourceRef> AddedSourceRefs,
    bool CreateReviewItem,
    bool CreateRegressionTest,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record MemoryProbeFeedbackResult(
    Guid FeedbackId,
    Guid SessionId,
    Guid TurnId,
    MemoryProbeFeedbackAction Action,
    IReadOnlyList<Guid> CreatedReviewItemIds,
    IReadOnlyList<Guid> CreatedGapRecordIds,
    IReadOnlyList<Guid> CreatedRegressionTestIds,
    IReadOnlyList<KnowledgeGapEvidenceRef> PublishedEvidenceRefs,
    IReadOnlyList<string> Warnings);

public sealed record MemoryRegressionExpectedConstraint(
    string ConstraintKind,
    string Description,
    bool Required,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record MemoryRegressionTestCaseRecord(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Question,
    RecallIntent Intent,
    MemoryProbeSessionMode ProbeMode,
    IReadOnlyList<MemoryRegressionExpectedConstraint> ExpectedConstraints,
    IReadOnlyList<Guid> RequiredMemoryItemIds,
    IReadOnlyList<Guid> ForbiddenMemoryItemIds,
    IReadOnlyList<string> ForbiddenClaims,
    MemoryAccessContext AccessContext,
    string EvaluatorProfile,
    Guid? CreatedFromProbeTurnId,
    MemoryRegressionTestState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record MemoryRegressionTestRunResult(
    Guid Id,
    Guid TestCaseId,
    Guid RecallTraceId,
    bool Passed,
    string Summary,
    IReadOnlyDictionary<string, string> Metrics,
    IReadOnlyList<string> Failures,
    DateTimeOffset CompletedAtUtc);

public interface IMemoryProbeSessionService
{
    Task<MemoryProbeSessionRecord> StartAsync(
        MemoryProbeSessionStartRequest request,
        CancellationToken cancellationToken = default);

    Task<MemoryProbeAnswerResult> AskAsync(
        MemoryProbeTurnRequest request,
        CancellationToken cancellationToken = default);

    Task<MemoryProbeFeedbackResult> SubmitFeedbackAsync(
        MemoryProbeFeedbackRequest request,
        CancellationToken cancellationToken = default);

    Task<MemoryProbeSessionRecord> CloseAsync(
        Guid sessionId,
        string reason,
        CancellationToken cancellationToken = default);
}

public interface IMemoryProbeQuestionGenerator
{
    Task<IReadOnlyList<MemoryProbeQuestion>> GenerateAsync(
        MemoryProbeQuestionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IMemoryProbeAssessmentService
{
    Task<IReadOnlyList<MemoryProbeFinding>> AssessAsync(
        MemoryProbeAnswerResult answer,
        CancellationToken cancellationToken = default);
}

public interface IMemoryRegressionTestService
{
    Task<MemoryRegressionTestCaseRecord> CreateFromProbeTurnAsync(
        Guid probeTurnId,
        IReadOnlyList<MemoryRegressionExpectedConstraint> constraints,
        CancellationToken cancellationToken = default);

    Task<MemoryRegressionTestRunResult> RunAsync(
        Guid testCaseId,
        CancellationToken cancellationToken = default);
}

public interface IMemoryProbeEvidencePublisher
{
    Task<IReadOnlyList<KnowledgeGapEvidenceRef>> PublishAsync(
        MemoryProbeFeedbackResult feedback,
        CancellationToken cancellationToken = default);
}

public static class MemoryProbingWorkflowExecutorKeys
{
    public const string StartSession = "memory.probe.session.start";
    public const string Ask = "memory.probe.ask";
    public const string GenerateQuestions = "memory.probe.generateQuestions";
    public const string Feedback = "memory.probe.feedback";
    public const string CreateRegressionTest = "memory.probe.regression.create";
    public const string RunRegressionTest = "memory.probe.regression.run";
    public const string ValidateLearning = "memory.probe.learning.validate";
}
