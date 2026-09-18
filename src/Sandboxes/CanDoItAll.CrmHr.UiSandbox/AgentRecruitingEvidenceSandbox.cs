using CanDoItAll.AgentFramework.Models;
using CanDoItAll.CrmHr.UI.Recruiting;
using CanDoItAll.Modules.CrmHr;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.CrmHr.UiSandbox;

public enum AgentRecruitingEvidenceSandboxScenario
{
    NoCandidate,
    Loading,
    Failed,
    Ready,
    CreateDialogOpen,
    AttachDialogOpen
}

public sealed record AgentRecruitingEvidenceSandboxContext(
    AgentRecruitingEvidenceSandboxScenario Scenario = AgentRecruitingEvidenceSandboxScenario.Ready,
    CrmHrHomeSandboxLayout Layout = CrmHrHomeSandboxLayout.Matched)
{
    public static AgentRecruitingEvidenceSandboxContext Parse(string? scenario, string? layout)
        => new(ParseScenario(scenario), CrmHrHomeSandboxContext.ParseLayout(layout));

    public static AgentRecruitingEvidenceSandboxScenario ParseScenario(string? token)
        => Enum.GetValues<AgentRecruitingEvidenceSandboxScenario>()
            .FirstOrDefault(scenario => string.Equals(Token(scenario), token?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Token(AgentRecruitingEvidenceSandboxScenario scenario)
        => string.Concat(scenario.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?>
    {
        ["scenario"] = Token(Scenario),
        ["layout"] = CrmHrHomeSandboxContext.Token(Layout)
    };
}

// Deterministic local state for the real agent assessment evidence surface. Nothing here calls a query service,
// persists, navigates or executes a real agent, workflow or process run; every attach or create appends to a local
// append-only interview list and logs the intent.
public sealed class AgentRecruitingEvidenceSandboxView : IAgentRecruitingEvidenceView
{
    private static readonly AgentDefinition SandboxCandidate = new(
        CrmHrSandboxData.RecruitingScoutAgent, "Recruiting Scout Agent", "Operations Copilot",
        "AI candidate in the recruiting pipeline, pending assessment review.",
        "You are Recruiting Scout Agent, an operations copilot candidate.", AgentLifecycleStatus.Draft, null, "",
        AgentWorkloadKind.Support, AgentChatHistoryMode.FrameworkManaged, 0.3, false, false, "{}", false, "",
        AgentPermissionsPolicy.Default, [], ["Candidate"], CrmHrSandboxData.BaseUpdatedAtUtc, CrmHrSandboxData.BaseUpdatedAtUtc);

    private static readonly IReadOnlyList<AgentDefinition> Evaluators =
    [
        new(CrmHrSandboxData.AtlasOpsAgent, "Atlas Ops Agent", "Operations Copilot", "Evaluator agent.", "Evaluate candidates.",
            AgentLifecycleStatus.Active, null, "gpt-5-mini", AgentWorkloadKind.Qa, AgentChatHistoryMode.FrameworkManaged, 0.2,
            false, false, "{}", false, "", AgentPermissionsPolicy.Default, [], [], CrmHrSandboxData.BaseUpdatedAtUtc, CrmHrSandboxData.BaseUpdatedAtUtc)
    ];

    private static readonly IReadOnlyList<ProviderProfile> ProviderProfiles =
    [
        new(CrmHrWorkspaceSandboxIds.Id("evidence-provider"), "Northwind Workspace Provider", ProviderKind.AzureOpenAi,
            "https://sandbox.example.test", "SANDBOX_PROVIDER_KEY", "gpt-5-mini", ProviderTransportKind.Responses,
            true, true, true, true, false, "{}", "Synthetic sandbox provider.", "Healthy", CrmHrSandboxData.BaseUpdatedAtUtc, ["gpt-5-mini"])
    ];

    private List<AgentRecruitingInterview> interviews = [];

    public Action? RenderRequested { get; set; }

    public string IntentLog { get; private set; } = "No intent yet.";

    public AgentDefinition? Candidate { get; private set; }

    public string? CandidateConfigurationVersion { get; private set; }

    public AgentRecruitingCandidateReadiness? Readiness { get; private set; }

    public IReadOnlyList<AgentRecruitingInterview> Interviews => interviews;

    public IReadOnlyList<AgentDefinition> EvaluatorAgents => Evaluators;

    public IReadOnlyList<ProviderProfile> Providers => ProviderProfiles;

    public string CreateCandidateName { get; set; } = "";

    public string CreatePurpose { get; set; } = "";

    public string LoadError { get; private set; } = "";

    public AttemptDraft AttemptDraft { get; private set; } = new();

    public bool IsLoading { get; private set; }

    public bool IsSaving { get; private set; }

    public bool IsCreateDialogOpen { get; private set; }

    public bool IsAttachDialogOpen { get; private set; }

    public Guid? CandidateAgentId => Candidate?.Id;

    public EventCallback<RecruitmentTrainingRequest> RequestTraining
        => EventCallback.Factory.Create<RecruitmentTrainingRequest>(this, HandleRequestTraining);

    public int AttemptCount => interviews.Sum(interview => interview.Attempts.Count);

    public AgentRecruitingAssessmentAnalysis? LatestAnalysis => interviews
        .SelectMany(interview => interview.Attempts)
        .OrderByDescending(attempt => attempt.CreatedAtUtc)
        .FirstOrDefault(attempt => attempt.Analysis is not null)?.Analysis;

    public void RequestRender() => RenderRequested?.Invoke();

    public void Apply(AgentRecruitingEvidenceSandboxScenario next)
    {
        IntentLog = "No intent yet.";
        IsLoading = false;
        IsSaving = false;
        IsCreateDialogOpen = false;
        IsAttachDialogOpen = false;
        LoadError = "";
        interviews = [];
        Candidate = SandboxCandidate;
        CandidateConfigurationVersion = "v1";

        switch (next)
        {
            case AgentRecruitingEvidenceSandboxScenario.NoCandidate:
                Candidate = null;
                CandidateConfigurationVersion = null;
                Readiness = null;
                break;
            case AgentRecruitingEvidenceSandboxScenario.Loading:
                IsLoading = true;
                Readiness = null;
                break;
            case AgentRecruitingEvidenceSandboxScenario.Failed:
                LoadError = "The candidate readiness and assessment evidence could not be loaded.";
                Readiness = null;
                break;
            case AgentRecruitingEvidenceSandboxScenario.CreateDialogOpen:
                SeedInterviews();
                CreateCandidateName = SandboxCandidate.Name;
                CreatePurpose = "";
                IsCreateDialogOpen = true;
                break;
            case AgentRecruitingEvidenceSandboxScenario.AttachDialogOpen:
                SeedInterviews();
                AttemptDraft = new AttemptDraft { InterviewId = interviews[0].Id };
                IsAttachDialogOpen = true;
                break;
            default:
                SeedInterviews();
                break;
        }
    }

    public Task ReloadAsync()
    {
        LoadError = "";
        SeedInterviews();
        Log("Reload assessment evidence");
        return Task.CompletedTask;
    }

    public Task OpenCreateDialog()
    {
        CreateCandidateName = Candidate?.Name ?? "";
        CreatePurpose = "";
        IsCreateDialogOpen = true;
        Log("Open create assessment dialog");
        return Task.CompletedTask;
    }

    public Task CloseCreateDialog()
    {
        IsCreateDialogOpen = false;
        Log("Close create assessment dialog");
        return Task.CompletedTask;
    }

    public Task OpenRecheckDialog(AgentRecruitingInterview interview, AgentRecruitingAttempt attempt)
    {
        AttemptDraft = new AttemptDraft
        {
            InterviewId = interview.Id,
            TargetKind = attempt.Target.Kind,
            ChallengeKey = attempt.ChallengeKey,
            ChallengeVersion = attempt.ChallengeVersion,
            RubricVersion = attempt.RubricVersion
        };
        IsAttachDialogOpen = true;
        Log($"Open recheck dialog: {interview.Purpose} / attempt {attempt.Sequence}");
        return Task.CompletedTask;
    }

    public Task CreateAssessmentAsync()
    {
        var interview = new AgentRecruitingInterview(CrmHrWorkspaceSandboxIds.Id($"evidence-interview-{Guid.NewGuid():N}"),
            Candidate?.Id ?? Guid.Empty, CandidateConfigurationVersion ?? "v1", CreateCandidateName, Candidate?.Model ?? "",
            CreatePurpose, DateTimeOffset.UtcNow, [], []);
        interviews.Insert(0, interview);
        IsCreateDialogOpen = false;
        Log($"Create assessment: {CreatePurpose}");
        return Task.CompletedTask;
    }

    public Task OpenAttachDialog()
    {
        AttemptDraft = new AttemptDraft { InterviewId = interviews.FirstOrDefault()?.Id };
        IsAttachDialogOpen = true;
        Log("Open attach result dialog");
        return Task.CompletedTask;
    }

    public Task CloseAttachDialog()
    {
        IsAttachDialogOpen = false;
        Log("Close attach result dialog");
        return Task.CompletedTask;
    }

    public Task AttachResultAsync()
    {
        var interviewIndex = interviews.FindIndex(item => item.Id == AttemptDraft.InterviewId);
        if (interviewIndex < 0)
        {
            Log("Attach result ignored: no assessment selected");
            return Task.CompletedTask;
        }

        var interview = interviews[interviewIndex];
        var analysis = new AgentRecruitingAssessmentAnalysis(AttemptDraft.Classification, AttemptDraft.Confidence,
            AttemptDraft.AnalysisSummary, AttemptDraft.ProposedNextStep,
            AttemptDraft.Strengths.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            AttemptDraft.Gaps.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var evaluation = new AgentRecruitingAutomatedEvaluation(AttemptDraft.AutomatedDecision, AttemptDraft.Score,
            AttemptDraft.EvaluatorAgentId, AttemptDraft.ProviderProfileId, AttemptDraft.Model, AttemptDraft.RubricVersion,
            [], DateTimeOffset.UtcNow);
        var missingEvidence = string.IsNullOrWhiteSpace(AttemptDraft.InputHash) || string.IsNullOrWhiteSpace(AttemptDraft.OutputHash)
            ? new[] { "Missing input/output hash." }
            : [];
        var attempt = new AgentRecruitingAttempt(CrmHrWorkspaceSandboxIds.Id($"attempt-{Guid.NewGuid():N}"), interview.Id,
            interview.Attempts.Count + 1, new AgentRecruitingExecutionTarget(AttemptDraft.TargetKind,
                Guid.TryParse(AttemptDraft.TargetId, out var targetId) ? targetId : Guid.NewGuid()), AttemptDraft.ChallengeKey,
            AttemptDraft.ChallengeVersion, AttemptDraft.RubricVersion, AttemptDraft.InputHash, AttemptDraft.OutputHash,
            AttemptDraft.StructuredOutputContractKey, AttemptDraft.StructuredOutputSchemaHash, "Validated", evaluation,
            missingEvidence.Length == 0 ? AgentRecruitingEvidenceCompleteness.Complete : AgentRecruitingEvidenceCompleteness.Incomplete,
            missingEvidence, DateTimeOffset.UtcNow, analysis);
        interviews[interviewIndex] = interview with { Attempts = [.. interview.Attempts, attempt] };
        RefreshReadiness();
        IsAttachDialogOpen = false;
        Log($"Attach result: {interview.Purpose} / attempt {attempt.Sequence}");
        return Task.CompletedTask;
    }

    public string FormatLatestScore()
    {
        var latest = interviews.SelectMany(interview => interview.Attempts).OrderByDescending(attempt => attempt.CreatedAtUtc).FirstOrDefault();
        return latest?.AutomatedEvaluation?.Score is { } score ? score.ToString("0.#") : "Not scored";
    }

    public string FormatLatestNextStep()
        => LatestAnalysis is { } analysis ? AgentRecruitingEvidencePresentation.FormatNextStep(analysis.ProposedNextStep) : "Not proposed";

    private void HandleRequestTraining(RecruitmentTrainingRequest request)
        => Log($"Request training: interview {request.InterviewId:D} / attempt {request.AttemptId:D}");

    private void SeedInterviews()
    {
        var attempt = new AgentRecruitingAttempt(CrmHrWorkspaceSandboxIds.Id("evidence-attempt-1"),
            CrmHrWorkspaceSandboxIds.Id("evidence-interview-1"), 1,
            new AgentRecruitingExecutionTarget(AgentRecruitingTargetKind.AgentExecutionRun, CrmHrWorkspaceSandboxIds.Id("evidence-run-1")),
            "crm-hr-assessment", "v1", "v1", "hash-input-1", "hash-output-1", "crmhr.evidence.v1", "hash-schema-1",
            "Validated",
            new AgentRecruitingAutomatedEvaluation(AgentRecruitingAutomatedDecision.NeedsHumanReview, 72m,
                CrmHrSandboxData.AtlasOpsAgent, ProviderProfiles[0].Id, "gpt-5-mini", "v1", ["Handles routine tickets well."],
                CrmHrSandboxData.BaseUpdatedAtUtc),
            AgentRecruitingEvidenceCompleteness.Complete, [], CrmHrSandboxData.BaseUpdatedAtUtc,
            new AgentRecruitingAssessmentAnalysis(AgentRecruitingAssessmentClassification.Suitable, 0.72m,
                "Handles routine operations tasks reliably; escalation judgment needs more evidence.",
                AgentRecruitingProposedNextStep.RequestHumanReview, ["Reliable on routine tasks"], ["Escalation judgment unproven"]));
        interviews =
        [
            new AgentRecruitingInterview(CrmHrWorkspaceSandboxIds.Id("evidence-interview-1"), SandboxCandidate.Id, "v1",
                SandboxCandidate.Name, SandboxCandidate.Model, "Operations readiness assessment", CrmHrSandboxData.BaseUpdatedAtUtc,
                [attempt], [])
        ];
        RefreshReadiness();
    }

    private void RefreshReadiness()
    {
        var attempts = interviews.SelectMany(interview => interview.Attempts).ToList();
        var history = attempts.Select(item => new AgentRecruitingAttemptComparison(item.Id, item.Sequence, item.CreatedAtUtc,
            item.Completeness, item.AutomatedEvaluation?.Decision, item.AutomatedEvaluation?.Score, null)).ToList();
        var status = attempts.Count == 0
            ? AgentRecruitingReadinessStatus.NoInterviews
            : attempts.Any(item => item.Completeness == AgentRecruitingEvidenceCompleteness.Incomplete)
                ? AgentRecruitingReadinessStatus.IncompleteEvidence
                : AgentRecruitingReadinessStatus.AwaitingHumanApproval;
        Readiness = new AgentRecruitingCandidateReadiness(Candidate?.Id ?? Guid.Empty, CandidateConfigurationVersion ?? "v1",
            status, false, false, true, interviews.FirstOrDefault()?.Id, attempts.FirstOrDefault()?.Id, null, "", "",
            ["Human review is required before this candidate reaches production readiness."], history);
    }

    private void Log(string message) => IntentLog = message;
}
