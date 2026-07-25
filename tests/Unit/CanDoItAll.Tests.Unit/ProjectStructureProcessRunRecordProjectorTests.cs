using System.Text.Json;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureProcessRunRecordProjectorTests
{
    private static readonly DateTimeOffset EndedAtUtc =
        new(2026, 7, 24, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LoadAsync_pages_and_adapts_completed_records_for_project_structure()
    {
        var projectId = Guid.NewGuid();
        var first = CreateSummary(
            projectId,
            ProcessRunDisposition.Succeeded,
            ProcessRunFactsStatus.Completed,
            narrative: CreateNarrative());
        var second = CreateSummary(
            projectId,
            ProcessRunDisposition.Cancelled,
            ProcessRunFactsStatus.Completed);
        var cursor = new ProcessRunRecordCursor(first.Metrics.EndedAtUtc, first.Identity.RunId);
        var reader = new RecordingReader(
            new ProcessRunRecordPage([first], cursor),
            new ProcessRunRecordPage([second], NextCursor: null));
        var projector = new ProjectStructureProcessRunRecordProjector(
            reader,
            NullLogger<ProjectStructureProcessRunRecordProjector>.Instance);

        var projections = await projector.LoadAsync(projectId, CancellationToken.None);

        Assert.Equal(2, projections.Count);
        var projection = projections[first.Identity.RunId.Value];
        Assert.Equal(first.Identity.RootRunId.Value, projection.RootRunId);
        Assert.Equal(first.Identity.PlanId?.Value, projection.PlanId);
        Assert.Equal(first.Identity.DefinitionId?.Value, projection.DefinitionId);
        Assert.Equal(ProcessRuntimeStatus.Completed, projection.RuntimeStatus);
        Assert.Equal("Succeeded", projection.Status);
        Assert.Equal(5, projection.Stats.TotalStepCount);
        Assert.Equal(4, projection.Stats.CompletedStepCount);
        Assert.Contains("4/5 steps", projection.Subtitle, StringComparison.Ordinal);
        Assert.Contains("1,525 tokens", projection.Subtitle, StringComparison.Ordinal);
        Assert.Contains("Manager summary", projection.Notes, StringComparison.Ordinal);
        Assert.Contains("agent:manager", projection.Notes, StringComparison.Ordinal);
        using var metadata = JsonDocument.Parse(projection.MetadataJson);
        Assert.Equal(
            "Completed",
            metadata.RootElement
                .GetProperty("processRunSummary")
                .GetProperty("FactsStatus")
                .GetString());

        Assert.Collection(
            reader.Queries,
            query =>
            {
                Assert.Equal(ProcessRunRecordPayloadLimits.MaximumPageSize, query.Take);
                Assert.Equal(ProcessRunRecordListPayload.Full, query.Payload);
                Assert.Equal(projectId, query.ProjectId);
                Assert.True(query.RootRunsOnly);
                Assert.Null(query.Cursor);
            },
            query =>
            {
                Assert.Equal(ProcessRunRecordPayloadLimits.MaximumPageSize, query.Take);
                Assert.Equal(cursor, query.Cursor);
            });
    }

    [Fact]
    public async Task LoadAsync_renders_failed_facts_without_claiming_completed_details()
    {
        var projectId = Guid.NewGuid();
        var summary = CreateSummary(
            projectId,
            ProcessRunDisposition.Failed,
            ProcessRunFactsStatus.Failed,
            factsLastErrorClass: "FactsAssemblyFailure",
            factsLastErrorDiagnosticReference: "diagnostic-42");
        var projector = new ProjectStructureProcessRunRecordProjector(
            new RecordingReader(new ProcessRunRecordPage([summary], NextCursor: null)),
            NullLogger<ProjectStructureProcessRunRecordProjector>.Instance);

        var projections = await projector.LoadAsync(projectId, CancellationToken.None);

        var projection = Assert.Single(projections).Value;
        Assert.Equal(ProcessRuntimeStatus.Failed, projection.RuntimeStatus);
        Assert.Equal("Failed · durable facts failed", projection.Subtitle);
        Assert.Contains("Facts status: Failed;", projection.Notes, StringComparison.Ordinal);
        Assert.Contains("No automatic retry remains.", projection.Notes, StringComparison.Ordinal);
        Assert.Contains("FactsAssemblyFailure", projection.Notes, StringComparison.Ordinal);
        Assert.Contains("diagnostic-42", projection.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("Manager summary", projection.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_throws_when_paging_cursor_does_not_advance()
    {
        var projectId = Guid.NewGuid();
        var cursor = new ProcessRunRecordCursor(EndedAtUtc, ProcessRunId.New());
        var reader = new RecordingReader(
            new ProcessRunRecordPage([], cursor),
            new ProcessRunRecordPage([], cursor));
        var projector = new ProjectStructureProcessRunRecordProjector(
            reader,
            NullLogger<ProjectStructureProcessRunRecordProjector>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => projector.LoadAsync(projectId, CancellationToken.None));

        Assert.Contains("pagination did not advance", exception.Message, StringComparison.Ordinal);
        Assert.Contains(projectId.ToString("D"), exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, reader.Queries.Count);
    }

    [Fact]
    public void Process_run_record_reader_contract_is_read_only()
    {
        var method = Assert.Single(typeof(IProcessRunRecordReader).GetMethods());

        Assert.Equal(nameof(IProcessRunRecordReader.ListAsync), method.Name);
        Assert.DoesNotContain(
            typeof(IProcessRunRecordReader).GetMethods(),
            candidate =>
                candidate.Name.Contains("Claim", StringComparison.Ordinal) ||
                candidate.Name.Contains("Complete", StringComparison.Ordinal) ||
                candidate.Name.Contains("Fail", StringComparison.Ordinal) ||
                candidate.Name.Contains("Upsert", StringComparison.Ordinal));
    }

    private static ProcessRunRecordSummary CreateSummary(
        Guid projectId,
        ProcessRunDisposition disposition,
        ProcessRunFactsStatus factsStatus,
        ProcessRunNarrative? narrative = null,
        string? factsLastErrorClass = null,
        string? factsLastErrorDiagnosticReference = null)
    {
        var runId = ProcessRunId.New();
        var participantId = new ProcessRunParticipantId("agent:manager");
        return new ProcessRunRecordSummary(
            new ProcessRunRecordIdentity(
                runId,
                runId,
                ParentRunId: null,
                ProcessInstancePlanId.New(),
                ProcessDefinitionId.New(),
                DefinitionVersionId: null,
                projectId),
            disposition,
            ProcessRunRecordLifecycleState.Current,
            ProcessRunRecordCompleteness.Partial,
            ProcessRunEvidenceSource.RuntimeState | ProcessRunEvidenceSource.UsageTelemetry,
            ProcessRunEvidenceSource.ArtifactLineage,
            [ProcessRunRecordWarningCode.MissingArtifactLineage],
            factsStatus,
            FactsAttemptCount: 2,
            FactsNextAttemptAtUtc: null,
            factsLastErrorClass,
            factsLastErrorDiagnosticReference,
            narrative is null
                ? ProcessRunNarrativeStatus.Pending
                : ProcessRunNarrativeStatus.Completed,
            NarrativeAttemptCount: narrative is null ? 0 : 1,
            NarrativeNextAttemptAtUtc: null,
            NarrativeLastErrorClass: null,
            NarrativeLastErrorDiagnosticReference: null,
            new ProcessRunRecordMetrics(
                StartedAtUtc: EndedAtUtc.AddMinutes(-2),
                EndedAtUtc,
                DurationMilliseconds: 120_000,
                TotalStepCount: 5,
                ExecutableStepCount: 5,
                CompletedStepCount: 4,
                FailedStepCount: disposition == ProcessRunDisposition.Failed ? 1 : 0,
                CancelledStepCount: disposition == ProcessRunDisposition.Cancelled ? 1 : 0,
                RepetitionCount: 1,
                ExecutionCount: 4,
                ReworkCount: 1,
                IncidentCount: disposition == ProcessRunDisposition.Failed ? 1 : 0,
                EscalationCount: 0,
                InputTokenCount: 1_000,
                CachedInputTokenCount: 100,
                OutputTokenCount: 400,
                ReasoningTokenCount: 25,
                TotalTokenCount: 1_525,
                EstimatedCost: 1.5m,
                ActualCost: 1.25m,
                ToolCallCount: 7,
                ArtifactCount: 3,
                SubprocessCount: 1),
            [participantId],
            narrative,
            SourceGlobalSequence: 42,
            SourceRootSequence: 10,
            ProcessRunRecordSchema.CurrentVersion,
            UpdatedAtUtc: EndedAtUtc.AddMinutes(1));
    }

    private static ProcessRunNarrative CreateNarrative()
    {
        var participantId = new ProcessRunParticipantId("agent:manager");
        return new ProcessRunNarrative(
            "The run completed.",
            "Succeeded",
            ["Prepared inputs.", "Produced output."],
            [],
            ["Used the validated plan."],
            [],
            new ProcessRunNarrativeProvenance(
                participantId,
                Guid.NewGuid(),
                "manager-summary-v1",
                "test-model",
                EndedAtUtc));
    }

    private sealed class RecordingReader(params ProcessRunRecordPage[] pages) : IProcessRunRecordReader
    {
        private readonly Queue<ProcessRunRecordPage> _pages = new(pages);

        public List<ProcessRunRecordListQuery> Queries { get; } = [];

        public Task<ProcessRunRecordPage> ListAsync(
            ProcessRunRecordListQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            return Task.FromResult(_pages.Dequeue());
        }
    }
}
