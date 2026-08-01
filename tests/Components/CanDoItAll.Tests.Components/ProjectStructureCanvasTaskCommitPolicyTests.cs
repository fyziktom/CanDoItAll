using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureCanvasTaskCommitPolicyTests
{
    [Fact]
    public void Create_commit_writes_not_started_execution_and_authoritative_pricing()
    {
        var person = new ProjectStructureTaskResourceSelection(
            ProjectStructureTaskResourceKind.Person,
            Guid.NewGuid());
        var quote = CreateQuote(
            320m,
            ProjectStructureTaskResourceCostSource.CrmWorkforceRate);
        var pricing = CreatePricing(person, quote);
        var request = new ProjectObjectCreateRequest(
            ProjectObjectType.WorkItem,
            "Prepare handoff",
            "Delivery",
            string.Empty,
            "project:11111111-1111-1111-1111-111111111111",
            ObjectSubtype: "task",
            MetadataJson: ProjectObjectMetadataSerializer.Serialize(
                new ProjectObjectMetadataEnvelope
                {
                    WorkItem = new ProjectWorkItemMetadata
                    {
                        WorkItemKind = ProjectWorkItemKind.Task,
                        ExpectedEffortHours = 8m,
                        ExpectedEffortUnit = ProjectWorkItemEffortUnit.Hours,
                        ExpectedCostAmount = 999m,
                        ExpectedCostCurrencyCode = "USD"
                    }
                }));

        var committed = ProjectStructureCanvasTaskCommitPolicy.ApplyCreate(
            request,
            pricing);

        var workItem = Assert.IsType<ProjectWorkItemMetadata>(
            ProjectObjectMetadataSerializer.Parse(committed.MetadataJson).WorkItem);
        Assert.Equal(ProjectTaskExecutionState.NotStarted, workItem.ExecutionState);
        Assert.Null(workItem.ActualStartedAtUtc);
        Assert.Null(workItem.ActualEndedAtUtc);
        Assert.Equal(320m, workItem.ExpectedCostAmount);
        Assert.Equal("USD", workItem.ExpectedCostCurrencyCode);
        Assert.Equal(person.ResourceId, workItem.ExpectedCostBasis!.ResourceId);
        Assert.Equal(
            ProjectStructureTaskResourceCostSource.CrmWorkforceRate,
            workItem.ExpectedCostBasis.Source);
    }

    [Fact]
    public void Read_legacy_task_without_work_item_metadata_fails_closed_as_unknown()
    {
        var snapshot = ProjectStructureCanvasTaskCommitPolicy.Read(
            CreateTask("{}"));

        Assert.Equal(ProjectTaskExecutionSnapshot.Unknown, snapshot.Execution);
        Assert.Equal(ProjectTaskEstimate.Empty(), snapshot.Estimate);
        Assert.Null(snapshot.CostBasis);
    }

    [Fact]
    public void Edit_commit_writes_explicit_execution_and_preserved_historical_cost()
    {
        var startedAt = DateTimeOffset.Parse("2026-07-23T12:00:00Z");
        var task = CreateTask(ProjectObjectMetadataSerializer.Serialize(
            new ProjectObjectMetadataEnvelope
            {
                WorkItem = new ProjectWorkItemMetadata
                {
                    WorkItemKind = ProjectWorkItemKind.Task,
                    ExecutionState = ProjectTaskExecutionState.NotStarted,
                    ExpectedEffortHours = 8m,
                    ExpectedEffortUnit = ProjectWorkItemEffortUnit.Hours,
                    ExpectedCostAmount = 120m,
                    ExpectedCostCurrencyCode = "USD"
                }
            }));
        var edit = new ProjectObjectEditRequest(
            "Updated task",
            "Delivery",
            string.Empty,
            null,
            null,
            task.MetadataJson);
        var pricing = new ProjectStructureTaskEstimateRefreshResult(
            new ProjectTaskEstimate(
                12m,
                ProjectWorkItemEffortUnit.Hours,
                120m,
                "USD"),
            ProjectStructureTaskEstimateRefreshStatus.Preserved,
            ProjectStructureTaskEstimateRefreshReason.ExecutionStateDoesNotAllowRefresh,
            null,
            null,
            null,
            false);

        var committed = ProjectStructureCanvasTaskCommitPolicy.ApplyEdit(
            task,
            edit,
            new ProjectTaskExecutionSnapshot(
                ProjectTaskExecutionState.Started,
                startedAt,
                null),
            pricing,
            costBasis: null);

        var workItem = Assert.IsType<ProjectWorkItemMetadata>(
            ProjectObjectMetadataSerializer.Parse(committed.MetadataJson).WorkItem);
        Assert.Equal(ProjectTaskExecutionState.Started, workItem.ExecutionState);
        Assert.Equal(startedAt, workItem.ActualStartedAtUtc);
        Assert.Null(workItem.ActualEndedAtUtc);
        Assert.Equal(12m, workItem.ExpectedEffortHours);
        Assert.Equal(120m, workItem.ExpectedCostAmount);
        Assert.Equal("USD", workItem.ExpectedCostCurrencyCode);
    }

    [Fact]
    public void Current_metadata_validation_accepts_non_pricing_metadata_changes()
    {
        var task = CreateTask(ProjectObjectMetadataSerializer.Serialize(
            new ProjectObjectMetadataEnvelope
            {
                WorkItem = new ProjectWorkItemMetadata
                {
                    WorkItemKind = ProjectWorkItemKind.Task,
                    ExecutionState = ProjectTaskExecutionState.NotStarted,
                    ExpectedEffortHours = 8m,
                    ExpectedEffortUnit = ProjectWorkItemEffortUnit.Hours,
                    ExpectedCostAmount = 120m,
                    ExpectedCostCurrencyCode = "USD"
                }
            }));
        var expected = ProjectStructureCanvasTaskCommitPolicy.Read(task);
        var currentMetadata = ProjectObjectMetadataSerializer.Parse(task.MetadataJson);
        currentMetadata.WorkItem!.AssigneePartyDisplayName = "Current assignee";

        ProjectStructureCanvasTaskCommitPolicy.ValidateCurrentMetadata(
            currentMetadata,
            expected);
    }

    [Fact]
    public void Current_metadata_validation_rejects_stale_pricing_snapshot()
    {
        var task = CreateTask(ProjectObjectMetadataSerializer.Serialize(
            new ProjectObjectMetadataEnvelope
            {
                WorkItem = new ProjectWorkItemMetadata
                {
                    WorkItemKind = ProjectWorkItemKind.Task,
                    ExecutionState = ProjectTaskExecutionState.NotStarted,
                    ExpectedEffortHours = 8m,
                    ExpectedEffortUnit = ProjectWorkItemEffortUnit.Hours,
                    ExpectedCostAmount = 120m,
                    ExpectedCostCurrencyCode = "USD"
                }
            }));
        var expected = ProjectStructureCanvasTaskCommitPolicy.Read(task);
        var currentMetadata = ProjectObjectMetadataSerializer.Parse(task.MetadataJson);
        currentMetadata.WorkItem!.ExpectedCostAmount = 180m;

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProjectStructureCanvasTaskCommitPolicy.ValidateCurrentMetadata(
                currentMetadata,
                expected));

        Assert.Contains("changed before save", exception.Message);
    }

    private static ProjectStructureTaskEstimateRefreshResult CreatePricing(
        ProjectStructureTaskResourceSelection resource,
        ProjectStructureTaskResourceCostQuote quote)
    {
        var basis = ProjectTaskExpectedCostBasisPolicy.Create(resource, quote);
        return new ProjectStructureTaskEstimateRefreshResult(
            new ProjectTaskEstimate(
                8m,
                ProjectWorkItemEffortUnit.Hours,
                quote.Amount,
                quote.CurrencyCode),
            ProjectStructureTaskEstimateRefreshStatus.Refreshed,
            ProjectStructureTaskEstimateRefreshReason.AuthoritativeQuoteApplied,
            resource,
            quote,
            basis,
            true);
    }

    private static ProjectStructureTaskResourceCostQuote CreateQuote(
        decimal amount,
        ProjectStructureTaskResourceCostSource source)
        => new(
            ProjectStructureTaskResourceCostQuoteStatus.Available,
            amount,
            "USD",
            "CRM workforce rate",
            "Calculated from the current CRM rate.",
            DateTimeOffset.Parse("2026-07-23T12:00:00Z"),
            source);

    private static ProjectStructureNode CreateTask(string metadataJson)
        => new(
            Id: "custom:task-a",
            ParentId: null,
            ObjectType: ProjectObjectType.WorkItem,
            ObjectSubtype: "task",
            Title: "Task",
            Subtitle: string.Empty,
            Status: "Planned",
            Notes: string.Empty,
            Route: string.Empty,
            ArtifactKind: string.Empty,
            ArtifactId: null,
            MediaRelativePath: string.Empty,
            MediaContentType: string.Empty,
            MediaOriginalFileName: string.Empty,
            X: 0,
            Y: 0,
            VisualProfile: new ProjectObjectVisualProfile(
                "pill",
                "#2563eb",
                "TK",
                "Task"),
            Badges: [],
            ProgressMode: string.Empty,
            ProgressPercent: 0,
            MarkerIcon: string.Empty,
            MarkerTone: string.Empty,
            MarkerLabel: string.Empty,
            Markers: [],
            Priority: 0)
        {
            MetadataJson = metadataJson
        };
}
