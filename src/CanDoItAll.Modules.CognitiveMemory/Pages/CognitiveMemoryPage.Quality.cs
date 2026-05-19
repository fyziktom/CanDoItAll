using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Common;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using System.Text;

namespace CanDoItAll.Modules.CognitiveMemory.Pages;

public partial class CognitiveMemoryPage
{
    internal const int ReviewUiPageSize = 12;

    internal static readonly IReadOnlyList<CognitiveMemoryConsolidationMode> QualityDreamModes =
    [
        CognitiveMemoryConsolidationMode.ProjectNightly,
        CognitiveMemoryConsolidationMode.CrossProjectWeekly,
        CognitiveMemoryConsolidationMode.ProcedureMining,
        CognitiveMemoryConsolidationMode.FailureLearning,
        CognitiveMemoryConsolidationMode.KnowledgeCoverageRefresh,
        CognitiveMemoryConsolidationMode.EpistemicDriveScan,
        CognitiveMemoryConsolidationMode.LearningOpportunityReview
    ];

    [Inject]
    public ICognitiveMemoryQualityDiagnosticsService QualityDiagnosticsService { get; set; } = default!;

    [Inject]
    public ICognitiveMemoryClusterPlanner ClusterPlanner { get; set; } = default!;

    [Inject]
    public ICognitiveMemoryDreamConsolidationService DreamConsolidationService { get; set; } = default!;

    [Inject]
    public ICognitiveMemoryAggregateMemoryApplicator AggregateMemoryApplicator { get; set; } = default!;

    internal readonly Dictionary<CognitiveMemoryReviewUiCollectionKind, int> pageIndexes = [];
    internal CognitiveMemoryQualityDiagnosticsReport? qualityDiagnosticsReport;
    internal CognitiveMemoryClusterPlanningResult? lastClusterPlanningResult;
    internal CognitiveMemoryDreamRunResult? lastDreamRunResult;
    internal CognitiveMemoryAggregateMemoryApplyResult? lastAggregateApplyResult;
    internal CognitiveMemoryConsolidationMode qualityDreamMode = CognitiveMemoryConsolidationMode.ProjectNightly;
    internal int qualityMaxRecords = 500;
    internal int qualityMaxClusters = 25;
    internal int qualityMinMembers = 2;
    internal bool qualityPersistDream = true;
    internal string qualityIdempotencyKey = string.Empty;
    internal string qualityOperationStatus = "Ready.";
    internal int qualityOperationProgress;
    internal Guid? selectedAggregateCandidateId;

    internal CognitiveMemoryAggregateCandidateView? SelectedAggregateCandidate
        => selectedAggregateCandidateId is { } candidateId
            ? snapshot?.AggregateCandidates.FirstOrDefault(candidate => candidate.Id.Value == candidateId)
            : null;

    internal bool CanApplySelectedAggregate
        => !isBusy &&
           SelectedAggregateCandidate is
           {
               Status: CognitiveMemoryDreamAggregateCandidateStatus.Approved,
               MemoryRecordId: null
           };

    internal string QualityBadgeText
    {
        get
        {
            if (snapshot is null)
            {
                return "0";
            }

            return (snapshot.Summary.QualityClusterCount +
                    snapshot.Summary.DreamRunCount +
                    snapshot.Summary.AggregateCandidateCount).ToString();
        }
    }

    internal CognitiveMemoryReviewUiQuery CreateSnapshotQuery()
        => new(
            ProjectId,
            IncludeResolvedReviewItems: true,
            PageRequests: BuildPageRequests());

    internal static Guid? ResolveSelectedAggregateCandidateId(
        CognitiveMemoryReviewUiSnapshot snapshot,
        Guid? preferredId)
    {
        if (preferredId.HasValue &&
            snapshot.AggregateCandidates.Any(candidate => candidate.Id.Value == preferredId.Value))
        {
            return preferredId.Value;
        }

        return snapshot.AggregateCandidates.FirstOrDefault()?.Id.Value;
    }

    internal IReadOnlyList<CognitiveMemoryReviewUiPageRequest> BuildPageRequests()
        => Enum.GetValues<CognitiveMemoryReviewUiCollectionKind>()
            .Select(collectionKind => new CognitiveMemoryReviewUiPageRequest(
                collectionKind,
                pageIndexes.GetValueOrDefault(collectionKind),
                ReviewUiPageSize))
            .ToArray();

    internal async Task<CognitiveMemoryReviewUiSnapshot> LoadSnapshotAsync()
    {
        var loaded = await ReviewUiService.GetSnapshotAsync(CreateSnapshotQuery(), CancellationToken.None);
        if (NormalizePageIndexes(loaded))
        {
            loaded = await ReviewUiService.GetSnapshotAsync(CreateSnapshotQuery(), CancellationToken.None);
        }

        return loaded;
    }

    internal bool NormalizePageIndexes(CognitiveMemoryReviewUiSnapshot loaded)
    {
        var changed = false;
        foreach (var page in loaded.Paging.Pages)
        {
            if (pageIndexes.GetValueOrDefault(page.CollectionKind) == page.PageIndex)
            {
                continue;
            }

            pageIndexes[page.CollectionKind] = page.PageIndex;
            changed = true;
        }

        return changed;
    }

    internal CognitiveMemoryReviewUiPageInfo PageInfo(CognitiveMemoryReviewUiCollectionKind collectionKind)
        => snapshot?.Paging.PageFor(collectionKind) ??
           new CognitiveMemoryReviewUiPageInfo(collectionKind, 0, ReviewUiPageSize, 0);

    internal string PageRangeText(CognitiveMemoryReviewUiCollectionKind collectionKind)
    {
        var page = PageInfo(collectionKind);
        if (page.TotalCount == 0)
        {
            return "0 records";
        }

        return $"{page.FirstRowNumber}-{page.LastRowNumber} of {page.TotalCount}";
    }

    internal async Task MovePageAsync(
        CognitiveMemoryReviewUiCollectionKind collectionKind,
        int delta)
    {
        if (snapshot is null || isBusy || delta == 0)
        {
            return;
        }

        var page = PageInfo(collectionKind);
        var maxPageIndex = Math.Max(0, page.TotalPages - 1);
        var nextPageIndex = Math.Clamp(page.PageIndex + delta, 0, maxPageIndex);
        if (nextPageIndex == page.PageIndex)
        {
            return;
        }

        pageIndexes[collectionKind] = nextPageIndex;
        await RefreshAsync();
    }

    internal RenderFragment CollectionPager(CognitiveMemoryReviewUiCollectionKind collectionKind)
        => builder =>
        {
            var page = PageInfo(collectionKind);
            if (page.TotalCount == 0)
            {
                return;
            }

            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "cognitive-memory-pager");
            builder.AddAttribute(2, "data-testid", $"cognitive-memory-pager-{collectionKind}");
            builder.OpenElement(3, "span");
            builder.AddContent(4, PageRangeText(collectionKind));
            builder.CloseElement();
            builder.OpenElement(5, "div");
            builder.AddAttribute(6, "class", "cognitive-memory-pager-actions");
            builder.OpenComponent<Button>(7);
            builder.AddAttribute(8, nameof(Button.Icon), "chevron_left");
            builder.AddAttribute(9, nameof(Button.ButtonStyle), ButtonStyle.Light);
            builder.AddAttribute(10, nameof(Button.Size), ButtonSize.Small);
            builder.AddAttribute(11, nameof(Button.Disabled), isBusy || !page.CanMovePrevious);
            builder.AddAttribute(12, nameof(Button.Click), EventCallback.Factory.Create(this, () => MovePageAsync(collectionKind, -1)));
            builder.AddAttribute(13, "title", "Previous page");
            builder.AddAttribute(14, "aria-label", $"Previous {FormatLabel(collectionKind)} page");
            builder.CloseComponent();
            builder.OpenComponent<Button>(15);
            builder.AddAttribute(16, nameof(Button.Icon), "chevron_right");
            builder.AddAttribute(17, nameof(Button.ButtonStyle), ButtonStyle.Light);
            builder.AddAttribute(18, nameof(Button.Size), ButtonSize.Small);
            builder.AddAttribute(19, nameof(Button.Disabled), isBusy || !page.CanMoveNext);
            builder.AddAttribute(20, nameof(Button.Click), EventCallback.Factory.Create(this, () => MovePageAsync(collectionKind, 1)));
            builder.AddAttribute(21, "title", "Next page");
            builder.AddAttribute(22, "aria-label", $"Next {FormatLabel(collectionKind)} page");
            builder.CloseComponent();
            builder.CloseElement();
            builder.CloseElement();
        };

    internal void SelectAggregateCandidate(Guid candidateId)
    {
        selectedAggregateCandidateId = candidateId;
        BumpUiRevision();
    }

    internal async Task RunQualityDiagnosticsAsync()
    {
        if (isBusy)
        {
            return;
        }

        await RunQualityOperationAsync(
            "Quality diagnostics",
            async () =>
            {
                qualityOperationProgress = 25;
                qualityOperationStatus = "Scanning source, cluster, dream, validation, and synthesis rows.";
                qualityDiagnosticsReport = await QualityDiagnosticsService.CreateReportAsync(
                    new CognitiveMemoryQualityDiagnosticsRequest(ProjectId, CreateQualityPolicyContext(CognitiveMemoryRiskLevel.Low)),
                    CancellationToken.None);
                qualityOperationProgress = 100;
                qualityOperationStatus = BuildQualityDiagnosticsStatus(qualityDiagnosticsReport);
            },
            reloadSnapshot: true);
    }

    internal async Task PlanQualityClustersAsync()
    {
        if (isBusy)
        {
            return;
        }

        if (!TryValidateQualityBudgets())
        {
            return;
        }
        await RunQualityOperationAsync(
            "Quality cluster planning",
            async () =>
            {
                qualityOperationProgress = 20;
                qualityOperationStatus = "Planning source-backed quality clusters.";
                lastClusterPlanningResult = await ClusterPlanner.PlanAsync(new CognitiveMemoryClusterPlanningRequest(
                    ProjectId,
                    CreateQualityPolicyContext(CognitiveMemoryRiskLevel.Medium),
                    minMembers: qualityMinMembers,
                    maxRecords: qualityMaxRecords,
                    persistClusters: true));
                qualityOperationProgress = 100;
                qualityOperationStatus = $"{lastClusterPlanningResult.Clusters.Count} cluster(s), {lastClusterPlanningResult.Metrics.MembersLinked} member link(s), {lastClusterPlanningResult.Warnings.Count} warning(s).";
            },
            reloadSnapshot: true);
    }

    internal async Task RunQualityDreamAsync()
    {
        if (isBusy)
        {
            return;
        }

        if (!TryValidateQualityBudgets())
        {
            return;
        }
        var idempotencyKey = string.IsNullOrWhiteSpace(qualityIdempotencyKey)
            ? $"quality-ui:{Guid.NewGuid():N}"
            : qualityIdempotencyKey.Trim();

        await RunQualityOperationAsync(
            "Dream consolidation",
            async () =>
            {
                qualityOperationProgress = 20;
                qualityOperationStatus = $"Running {FormatLabel(qualityDreamMode).ToLowerInvariant()} dream consolidation.";
                lastDreamRunResult = await DreamConsolidationService.RunAsync(new CognitiveMemoryDreamRunRequest(
                    ProjectId,
                    qualityDreamMode,
                    CognitiveMemoryConsolidationTriggerKind.Manual,
                    CreateQualityPolicyContext(CognitiveMemoryRiskLevel.Medium),
                    new CognitiveMemoryIdempotencyKey(idempotencyKey),
                    maxClusters: qualityMaxClusters,
                    minMembersPerCluster: qualityMinMembers,
                    persistChanges: qualityPersistDream));
                qualityIdempotencyKey = idempotencyKey;
                qualityOperationProgress = 100;
                qualityOperationStatus = $"{FormatLabel(lastDreamRunResult.Status)}: {lastDreamRunResult.Metrics.ClustersConsidered} cluster(s), {lastDreamRunResult.Metrics.AggregateCandidatesCreated} aggregate candidate(s), {lastDreamRunResult.Warnings.Count} warning(s).";
            },
            reloadSnapshot: true);
    }

    internal async Task ApplySelectedAggregateAsync()
    {
        var candidate = SelectedAggregateCandidate;
        if (candidate is null || isBusy)
        {
            return;
        }

        await RunQualityOperationAsync(
            "Aggregate memory apply",
            async () =>
            {
                qualityOperationProgress = 25;
                qualityOperationStatus = $"Applying aggregate candidate {FormatShortId(candidate.Id.Value)}.";
                lastAggregateApplyResult = await AggregateMemoryApplicator.ApplyAsync(new CognitiveMemoryAggregateMemoryApplyRequest(
                    candidate.Id,
                    OperatorActorId,
                    CreateQualityPolicyContext(candidate.RiskLevel)));
                qualityOperationProgress = 100;
                qualityOperationStatus = $"{(lastAggregateApplyResult.Created ? "Created" : "Loaded")} memory {FormatShortId(lastAggregateApplyResult.MemoryRecordId.Value)} with {lastAggregateApplyResult.ClaimIds.Count} claim(s).";
            },
            reloadSnapshot: true);
    }

    internal async Task RunQualityOperationAsync(
        string operationName,
        Func<Task> operation,
        bool reloadSnapshot)
    {
        isBusy = true;
        errorMessage = string.Empty;
        qualityOperationProgress = 10;
        qualityOperationStatus = $"Starting {operationName.ToLowerInvariant()}.";
        BumpUiRevision();
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        try
        {
            await operation();
            NotificationService.Success($"{operationName} finished", qualityOperationStatus);
            if (reloadSnapshot)
            {
                await ReloadSnapshotAsync();
            }
        }
        catch (Exception exception)
        {
            qualityOperationProgress = 100;
            qualityOperationStatus = exception.Message;
            errorMessage = exception.Message;
            BumpUiRevision();
            NotificationService.Error($"{operationName} failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    internal CognitiveMemoryPolicyContext CreateQualityPolicyContext(CognitiveMemoryRiskLevel riskLevel)
        => new(
            ProjectId,
            OperatorActorId,
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("cognitive-memory-ui"),
            riskLevel,
            AllowRestrictedContent: false);

    internal void ValidateQualityBudgets()
    {
        if (qualityMinMembers < 2)
        {
            throw new InvalidOperationException("Minimum members must be at least 2.");
        }

        if (qualityMaxRecords <= 0)
        {
            throw new InvalidOperationException("Max records must be positive.");
        }

        if (qualityMaxClusters <= 0)
        {
            throw new InvalidOperationException("Max clusters must be positive.");
        }
    }

    internal bool TryValidateQualityBudgets()
    {
        try
        {
            ValidateQualityBudgets();
            return true;
        }
        catch (Exception exception)
        {
            qualityOperationProgress = 100;
            qualityOperationStatus = exception.Message;
            errorMessage = exception.Message;
            BumpUiRevision();
            NotificationService.Error("Quality operation failed", exception.Message);
            return false;
        }
    }

    internal static string BuildQualityDiagnosticsStatus(CognitiveMemoryQualityDiagnosticsReport report)
    {
        var status = $"{report.ClusterCount} cluster(s), {report.DreamRunCount} dream run(s), {report.AggregateCandidateCount} aggregate candidate(s), {report.SynthesizedRecallCount} synthesized recall(s).";
        return report.Warnings.Count == 0
            ? status
            : $"{status} {report.Warnings.Count} warning(s).";
    }
}
