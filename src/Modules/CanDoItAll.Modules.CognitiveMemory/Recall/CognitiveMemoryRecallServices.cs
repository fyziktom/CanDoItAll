using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed partial class CognitiveMemoryRecallOrchestrator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryEmbeddingProvider embeddingProvider,
    ICognitiveMemoryProjectionAdapter projectionAdapter,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    ICognitiveMemorySignalLedger signalLedger,
    ICognitiveMemoryWorkspaceService workspaceService,
    IClock clock,
    ILogger<CognitiveMemoryRecallOrchestrator> logger) : ICognitiveMemoryRecallOrchestrator
{
    private const int OverlapDeduplicationMinimumCharacters = 80;
    private const int LexicalFallbackScanLimit = 500;
    private const int LexicalTermLimit = 32;
    private const string WorkbenchProjectStructureSourceSystem = "WorkbenchProjectStructure";
    private const string ExternalFileSourceSystem = "ExternalFile";
    private const string ProjectNodeSourceItemType = "ProjectNode";
    private static readonly HashSet<string> LexicalStopWords = new(StringComparer.Ordinal)
    {
        "a",
        "about",
        "after",
        "and",
        "are",
        "as",
        "be",
        "but",
        "by",
        "detail",
        "details",
        "do",
        "does",
        "explain",
        "for",
        "from",
        "how",
        "include",
        "included",
        "includes",
        "including",
        "in",
        "is",
        "it",
        "not",
        "of",
        "or",
        "project",
        "projects",
        "require",
        "required",
        "requires",
        "should",
        "source",
        "summarize",
        "summary",
        "that",
        "the",
        "to",
        "truth",
        "was",
        "what",
        "when",
        "which",
        "who",
        "why",
        "with"
    };
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> LexicalTermAliases = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["certification"] = ["certifikace", "certifikační", "medical"],
        ["certifications"] = ["certifikace", "certifikační", "medical"],
        ["cost"] = ["náklad", "náklady", "nákladech", "pořizovací", "pořizovacích"],
        ["costs"] = ["náklad", "náklady", "nákladech", "pořizovací", "pořizovacích"],
        ["customer"] = ["zákazník", "zákazníka", "odběratel", "odběratelé"],
        ["customers"] = ["zákazník", "zákazníka", "odběratel", "odběratelé"],
        ["deployment"] = ["instalace", "spuštění", "provoz"],
        ["hospital"] = ["nemocnice", "nemocnici"],
        ["hospitals"] = ["nemocnice", "nemocnici"],
        ["price"] = ["cena", "ceny", "prodej", "prodejní"],
        ["pricing"] = ["cena", "ceny", "prodej", "prodejní"],
        ["purchase"] = ["pořizovací", "pořizovacích", "nákup", "náklad"],
        ["risk"] = ["riziko", "rizika", "threats", "weaknesses"],
        ["risks"] = ["riziko", "rizika", "threats", "weaknesses"],
        ["sale"] = ["prodej", "prodejní"],
        ["sales"] = ["prodej", "prodejní"],
        ["segment"] = ["trh", "trhy", "segment", "odběratel"],
        ["segments"] = ["trh", "trhy", "segment", "odběratel"],
        ["senior"] = ["senior", "seniory", "domovy"],
        ["seniors"] = ["senior", "seniory", "domovy"]
    };
    private static readonly Regex RecallEmailRegex = new(
        @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex RecallInternationalPhoneRegex = new(
        @"\+\d{1,3}(?:[\s.-]?\d){6,}\d",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async ValueTask<CognitiveMemoryRecallResult> RecallAsync(
        CognitiveMemoryRecallRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        var nowUtc = clock.GetUtcNow();
        var traceId = Guid.NewGuid();
        var queryTerms = NormalizeTerms(request.Query);
        var stages = new List<CognitiveMemoryRecallTraceStage>();
        var warnings = new List<string>();
        var workspaceFrameId = await ResolveWorkspaceFrameIdAsync(request, cancellationToken);
        var candidates = new Dictionary<Guid, RecallCandidateAccumulator>();

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.IntentAndScope,
            CognitiveMemoryRecallChannelKind.Unknown,
            CognitiveMemoryRecallStageStatus.Completed,
            candidateCount: 0,
            selectedCount: 0,
            excludedCount: 0,
            providerTrace: $"intent:{request.Intent}:mode:{request.Mode}",
            completedAtUtc: nowUtc));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await AddLexicalCandidatesAsync(dbContext, request, queryTerms, candidates, stages, nowUtc, cancellationToken);
        await AddVectorCandidatesAsync(dbContext, request, candidates, stages, warnings, nowUtc, cancellationToken);
        await AddWorkspaceCandidatesAsync(dbContext, workspaceFrameId, candidates, stages, nowUtc, cancellationToken);
        await AddSignalActivationCandidatesAsync(request, candidates, stages, warnings, nowUtc, cancellationToken);
        await AddGraphExpansionCandidatesAsync(dbContext, request, candidates, stages, nowUtc, cancellationToken);

        var evaluatedCandidates = await EvaluateCandidatesAsync(
            dbContext,
            traceId,
            request,
            workspaceFrameId,
            queryTerms,
            candidates.Values.ToList(),
            nowUtc,
            cancellationToken);
        var focusedCandidates = SelectFocus(evaluatedCandidates, request.Budget, warnings);

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.FocusSelection,
            CognitiveMemoryRecallChannelKind.Unknown,
            CognitiveMemoryRecallStageStatus.Completed,
            evaluatedCandidates.Count,
            focusedCandidates.Count(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected),
            focusedCandidates.Count(candidate => candidate.DecisionKind is CognitiveMemoryRecallCandidateDecisionKind.Excluded or CognitiveMemoryRecallCandidateDecisionKind.Inhibited),
            "focus:score-geometry",
            limitingBudget: focusedCandidates.FirstOrDefault(candidate => candidate.ExclusionReasonKind == CognitiveMemoryRecallExclusionReasonKind.BudgetLimit) is null ? null : CognitiveMemoryBudgetLimit.ItemCount,
            completedAtUtc: nowUtc));

        var contextPack = await BuildContextPackAsync(
            dbContext,
            traceId,
            request,
            workspaceFrameId,
            focusedCandidates,
            stages,
            warnings,
            nowUtc,
            cancellationToken);

        var selected = focusedCandidates.Where(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected).ToList();
        var excluded = focusedCandidates.Where(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Excluded).ToList();
        var inhibited = focusedCandidates.Where(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Inhibited).ToList();
        var selectedClaimCount = selected.SelectMany(candidate => candidate.SelectedClaimIds).Select(id => id.Value).Distinct().Count();
        var selectedEvidenceCount = contextPack.SourceRefs
            .Where(sourceRef => sourceRef.IncludedInContext && sourceRef.EvidenceAnchorId is not null)
            .Select(sourceRef => sourceRef.EvidenceAnchorId!.Value.Value)
            .Distinct()
            .Count();
        var limitingBudget = ResolveLimitingBudget(stages, focusedCandidates, contextPack.Warnings);
        var payload = new CognitiveMemoryRecallTracePayload(
            request.Mode,
            request.Intent,
            stages,
            warnings,
            request.Metadata ?? EmptyMetadata);
        var trace = new CognitiveMemoryRecallTraceRecord
        {
            Id = traceId,
            ProjectId = request.ProjectId,
            OperationMode = CognitiveMemoryOperationMode.Recall,
            RecallMode = request.Mode,
            RequestedByActorId = request.PolicyContext.ActorId,
            PolicyProfileId = request.PolicyContext.PolicyProfileId.Value,
            WorkspaceFrameId = workspaceFrameId?.Value,
            AttentionDecisionId = request.AttentionDecisionId?.Value,
            SelfRegulationAssessmentId = NormalizeOptional(request.SelfRegulationAssessmentId),
            AnswerPostureDecisionId = NormalizeOptional(request.AnswerPostureDecisionId),
            AnswerGateDecisionId = NormalizeOptional(request.AnswerGateDecisionId),
            ContextPackId = contextPack.Id.Value,
            RequestHashAlgorithm = CognitiveMemoryHashAlgorithm.Sha256,
            RequestHash = CognitiveMemoryHash.FromUtf8($"{request.ProjectId:D}:{request.Mode}:{request.Intent}:{request.Query}").Value,
            AlgorithmVersion = CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion.Value,
            Outcome = CognitiveMemoryRunStatus.Succeeded,
            IncludedRecordCount = selected.Count,
            ExcludedRecordCount = excluded.Count,
            SelectedClaimCount = selectedClaimCount,
            SelectedEvidenceAnchorCount = selectedEvidenceCount,
            InhibitedCandidateCount = inhibited.Count,
            LimitingBudget = limitingBudget,
            TraceJson = JsonSerializer.Serialize(payload, CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryRecallTracePayload),
            StartedAtUtc = nowUtc,
            CompletedAtUtc = clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };

        dbContext.Add(trace);
        AddStageRecords(dbContext, traceId, request.ProjectId, stages, nowUtc);
        AddCandidateRecords(dbContext, traceId, workspaceFrameId, focusedCandidates, contextPack, nowUtc);
        AddContextPackRecords(dbContext, traceId, contextPack, request.Budget.ContextCharacterBudget, nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CognitiveMemoryRecallResult(
            traceId,
            contextPack,
            focusedCandidates.Select(ToContract).ToList(),
            stages,
            warnings);
    }

    private async Task<CognitiveMemoryWorkspaceFrameId?> ResolveWorkspaceFrameIdAsync(
        CognitiveMemoryRecallRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WorkspaceFrameId is { } workspaceFrameId)
        {
            return workspaceFrameId;
        }

        if (request.WorkspaceOpenRequest is null)
        {
            return null;
        }

        var workspace = await workspaceService.GetOrCreateAsync(request.WorkspaceOpenRequest, cancellationToken);
        return new CognitiveMemoryWorkspaceFrameId(workspace.Frame.Id);
    }

    private static void ValidateRequest(CognitiveMemoryRecallRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CognitiveMemoryGuard.EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId));
        CognitiveMemoryGuard.EnsureText(request.Query, nameof(request.Query));
        if (request.Intent == CognitiveMemoryRecallIntentKind.Unknown)
        {
            throw new ArgumentException("Recall intent must be explicit.", nameof(request.Intent));
        }

        if (request.Mode == CognitiveMemoryRecallMode.Unknown)
        {
            throw new ArgumentException("Recall mode must be explicit.", nameof(request.Mode));
        }

        ArgumentNullException.ThrowIfNull(request.PolicyContext);
        if (request.PolicyContext.ProjectId is { } policyProjectId && policyProjectId != request.ProjectId)
        {
            throw new InvalidOperationException($"Recall policy project '{policyProjectId:D}' does not match request project '{request.ProjectId:D}'.");
        }

        if (string.IsNullOrWhiteSpace(request.PolicyContext.ActorId))
        {
            throw new ArgumentException("Recall policy actor id is required.", nameof(request.PolicyContext.ActorId));
        }
    }
}
