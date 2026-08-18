using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessWorkspaceProvenanceContractTests
{
    [Fact]
    public void Every_process_context_field_has_an_explicit_provenance_component()
    {
        var fields = Enum.GetValues<ProcessWorkspaceContextField>();

        var components = fields
            .Select(ProcessWorkspaceContextFieldProvenance.GetComponent)
            .ToArray();

        Assert.Equal(fields.Length, components.Length);
        Assert.All(components, component => Assert.True(Enum.IsDefined(component)));
        Assert.Equal(
            ProcessWorkspaceProvenanceComponent.Selection,
            ProcessWorkspaceContextFieldProvenance.GetComponent(
                ProcessWorkspaceContextField.RuntimeStatusFilter));
        Assert.Equal(
            ProcessWorkspaceProvenanceComponent.ShellRefresh,
            ProcessWorkspaceContextFieldProvenance.GetComponent(
                ProcessWorkspaceContextField.ProjectionStatus));
        Assert.Equal(
            ProcessWorkspaceProvenanceComponent.DefinitionCatalog,
            ProcessWorkspaceContextFieldProvenance.GetComponent(
                ProcessWorkspaceContextField.DefinitionCompatibilityIssues));
        Assert.Equal(
            ProcessWorkspaceProvenanceComponent.LiveRuns,
            ProcessWorkspaceContextFieldProvenance.GetComponent(
                ProcessWorkspaceContextField.CurrentStepRole));
        Assert.Equal(
            ProcessWorkspaceProvenanceComponent.HistoryPage,
            ProcessWorkspaceContextFieldProvenance.GetComponent(
                ProcessWorkspaceContextField.EventSensitivity));
        Assert.Equal(
            ProcessWorkspaceProvenanceComponent.ActiveAgents,
            ProcessWorkspaceContextFieldProvenance.GetComponent(
                ProcessWorkspaceContextField.AgentRole));
        Assert.Equal(
            ProcessWorkspaceProvenanceComponent.DerivedProjection,
            ProcessWorkspaceContextFieldProvenance.GetComponent(
                ProcessWorkspaceContextField.ManagerUsageTotalTokens));
    }

    [Fact]
    public void Changing_each_source_component_changes_only_that_vector_component()
    {
        var baseline = new ProcessWorkspaceProvenanceVector(
            CreatePresent(ProcessWorkspaceProvenanceComponent.Selection, "baseline"),
            CreatePresent(ProcessWorkspaceProvenanceComponent.ShellRefresh, "baseline"),
            CreatePresent(ProcessWorkspaceProvenanceComponent.DefinitionCatalog, "baseline"),
            CreatePresent(ProcessWorkspaceProvenanceComponent.LiveRunSummary, "baseline"),
            CreatePresent(ProcessWorkspaceProvenanceComponent.LiveRuns, "baseline"),
            CreatePresent(ProcessWorkspaceProvenanceComponent.SelectedRunDetail, "baseline"),
            CreatePresent(ProcessWorkspaceProvenanceComponent.SelectedRunRecord, "baseline"),
            CreatePresent(ProcessWorkspaceProvenanceComponent.HistoryPage, "baseline"),
            CreatePresent(ProcessWorkspaceProvenanceComponent.MetricHistory, "baseline"),
            CreatePresent(ProcessWorkspaceProvenanceComponent.ActiveAgents, "baseline"),
            CreatePresent(ProcessWorkspaceProvenanceComponent.UsageTelemetry, "baseline"),
            CreatePresent(ProcessWorkspaceProvenanceComponent.DerivedProjection, "baseline"));

        foreach (var component in Enum.GetValues<ProcessWorkspaceProvenanceComponent>())
        {
            var changed = SetComponent(
                baseline,
                component,
                CreatePresent(component, "changed"));
            var changedComponents = Enum
                .GetValues<ProcessWorkspaceProvenanceComponent>()
                .Where(candidate => baseline.GetComponent(candidate) != changed.GetComponent(candidate))
                .ToArray();

            Assert.Equal([component], changedComponents);
        }
    }

    [Fact]
    public void Durable_record_revision_rejects_invalid_sequences_and_timestamps()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProcessRunRecordProjectionRevision(-1, 0, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProcessRunRecordProjectionRevision(0, -1, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(
            () => new ProcessRunRecordProjectionRevision(0, 0, default));
        Assert.Throws<ArgumentException>(
            () => new ProcessRunRecordProjectionRevision(
                0,
                0,
                new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.FromHours(-4))));
    }

    [Fact]
    public void Component_and_vector_construction_enforce_provenance_invariants()
    {
        var fingerprint = ProcessProjectionContentFingerprintFactory.Create(
            ProcessWorkspaceProvenanceComponent.SelectedRunRecord,
            new FingerprintContent("record"));
        var revision = new ProcessRunRecordProjectionRevision(
            10,
            3,
            new DateTimeOffset(2026, 7, 27, 16, 0, 0, TimeSpan.Zero));

        Assert.Throws<ArgumentException>(
            () => ProcessProjectionComponentProvenance.Present(
                ProcessProjectionComponentSource.RunRecordStore,
                fingerprint));
        Assert.Throws<ArgumentException>(
            () => ProcessProjectionComponentProvenance.Present(
                ProcessProjectionComponentSource.ShellProjection,
                fingerprint,
                runRecordRevision: revision));
        Assert.Throws<ArgumentException>(
            () => ProcessProjectionComponentProvenance.Absent(
                ProcessProjectionComponentSource.RuntimeProjectionStore,
                ProcessProjectionComponentAbsenceReason.SourceUnavailable,
                fingerprint));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProcessProjectionComponentProvenance.Absent(
                ProcessProjectionComponentSource.RuntimeProjectionStore,
                (ProcessProjectionComponentAbsenceReason)999));
        Assert.Throws<ArgumentException>(
            () => ProcessProjectionComponentProvenance.Absent(
                ProcessProjectionComponentSource.RuntimeProjectionStore,
                ProcessProjectionComponentAbsenceReason.NoData,
                default(ProcessProjectionContentFingerprint)));
        Assert.Throws<ArgumentNullException>(
            () => ProcessWorkspaceProvenanceVector.Empty with
            {
                LiveRuns = null!
            });
    }

    [Fact]
    public void Canonical_fingerprint_is_property_order_independent_and_component_scoped()
    {
        var first = ProcessProjectionContentFingerprintFactory.Create(
            ProcessWorkspaceProvenanceComponent.LiveRuns,
            new Dictionary<string, int>
            {
                ["second"] = 2,
                ["first"] = 1
            });
        var reordered = ProcessProjectionContentFingerprintFactory.Create(
            ProcessWorkspaceProvenanceComponent.LiveRuns,
            new Dictionary<string, int>
            {
                ["first"] = 1,
                ["second"] = 2
            });
        var otherComponent = ProcessProjectionContentFingerprintFactory.Create(
            ProcessWorkspaceProvenanceComponent.SelectedRunDetail,
            new Dictionary<string, int>
            {
                ["first"] = 1,
                ["second"] = 2
            });

        Assert.Equal(first, reordered);
        Assert.NotEqual(first, otherComponent);
    }

    private static ProcessProjectionComponentProvenance CreatePresent(
        ProcessWorkspaceProvenanceComponent component,
        string value)
        => ProcessProjectionComponentProvenance.Present(
            ProcessProjectionComponentSource.ShellProjection,
            ProcessProjectionContentFingerprintFactory.Create(
                component,
                new FingerprintContent(value)));

    private static ProcessWorkspaceProvenanceVector SetComponent(
        ProcessWorkspaceProvenanceVector vector,
        ProcessWorkspaceProvenanceComponent component,
        ProcessProjectionComponentProvenance provenance)
        => component switch
        {
            ProcessWorkspaceProvenanceComponent.Selection => vector with { Selection = provenance },
            ProcessWorkspaceProvenanceComponent.ShellRefresh => vector with { ShellRefresh = provenance },
            ProcessWorkspaceProvenanceComponent.DefinitionCatalog => vector with { DefinitionCatalog = provenance },
            ProcessWorkspaceProvenanceComponent.LiveRunSummary => vector with { LiveRunSummary = provenance },
            ProcessWorkspaceProvenanceComponent.LiveRuns => vector with { LiveRuns = provenance },
            ProcessWorkspaceProvenanceComponent.SelectedRunDetail => vector with { SelectedRunDetail = provenance },
            ProcessWorkspaceProvenanceComponent.SelectedRunRecord => vector with { SelectedRunRecord = provenance },
            ProcessWorkspaceProvenanceComponent.HistoryPage => vector with { HistoryPage = provenance },
            ProcessWorkspaceProvenanceComponent.MetricHistory => vector with { MetricHistory = provenance },
            ProcessWorkspaceProvenanceComponent.ActiveAgents => vector with { ActiveAgents = provenance },
            ProcessWorkspaceProvenanceComponent.UsageTelemetry => vector with { UsageTelemetry = provenance },
            ProcessWorkspaceProvenanceComponent.DerivedProjection => vector with { DerivedProjection = provenance },
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, "The process provenance component is undefined.")
        };

    private sealed record FingerprintContent(string Value);
}
