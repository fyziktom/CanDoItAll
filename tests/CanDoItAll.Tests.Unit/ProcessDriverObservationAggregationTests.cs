using System.Runtime.CompilerServices;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.ObservationAggregation;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDriverObservationAggregationTests
{
    [Fact]
    public void Process_driver_observation_aggregation_combines_all_readonly_verifier_observations_without_mutation()
    {
        var responses = new[]
        {
            CreateResponse(
                ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
                accepted: true,
                ProcessDriverDiagnosticCategory.BuildWarning,
                ProcessDriverDiagnosticSeverity.Warning),
            CreateResponse(
                ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
                accepted: true,
                ProcessDriverDiagnosticCategory.RuntimeEvidenceInconsistent,
                ProcessDriverDiagnosticSeverity.Warning),
            CreateResponse(
                ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
                accepted: true,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                ProcessDriverDiagnosticSeverity.Warning),
            CreateResponse(
                ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
                accepted: true,
                ProcessDriverDiagnosticCategory.BusinessEvidenceGap,
                ProcessDriverDiagnosticSeverity.Warning,
                diagnosticMessage: "Business evidence gap for reviewer@example.invalid"),
            CreateResponse(
                ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
                accepted: false,
                ProcessDriverDiagnosticCategory.ArtifactLineageMissing,
                ProcessDriverDiagnosticSeverity.Error,
                new ProcessDriverRedactionDescriptor(
                    ProcessDriverRedactionStatus.Redacted,
                    [ProcessDriverRedactionKind.Secret],
                    ProcessDriverEvidencePolicy.ComputeSha256("[redacted-secret]")),
                diagnosticMessage: "Artifact lineage rejected with secret=fixture-value")
        };
        var aggregator = new ProcessDriverObservationAggregator();
        var result = aggregator.Aggregate(new ProcessDriverObservationAggregationRequest(
            responses,
            DateTimeOffset.Parse("2026-06-08T15:00:00Z"),
            "manager:observation-aggregate"));

        Assert.Equal(DateTimeOffset.Parse("2026-06-08T15:00:00Z"), result.RequestedAt);
        Assert.Equal("manager:observation-aggregate", result.CallerContext);
        Assert.Equal(5, result.ResponseCount);
        Assert.Equal(4, result.AcceptedCount);
        Assert.Equal(1, result.DeniedCount);
        Assert.Equal(5, result.DiagnosticCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(4, result.WarningCount);
        Assert.True(result.AggregationMutationFree);
        Assert.True(result.AllResponsesMutationFree);
        Assert.Equal(ProcessDriverContractVersion.Current, result.ContractVersion);
        Assert.Equal(ProcessDriverRedactionStatus.Redacted, result.Redaction.Status);
        Assert.Contains(ProcessDriverRedactionKind.Secret, result.Redaction.AppliedKinds);
        Assert.Contains(ProcessDriverRedactionKind.EmailAddress, result.Redaction.AppliedKinds);
        Assert.True(ProcessDriverEvidencePolicy.IsSha256(result.Redaction.RedactedTextHash));
        Assert.Equal(5, result.EvidenceReferences.Count);
        Assert.All(result.EvidenceReferences, evidenceReference =>
            Assert.True(ProcessDriverEvidencePolicy.IsSha256(evidenceReference.ContentHash)));

        var laneSummaries = result.LaneSummaries.ToDictionary(summary => summary.Lane);

        Assert.Equal(5, laneSummaries.Count);
        Assert.Contains(ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification, laneSummaries.Keys);
        Assert.Contains(ProcessDriverCapabilityScopeKind.RuntimeFactsRead, laneSummaries.Keys);
        Assert.Contains(ProcessDriverCapabilityScopeKind.OfficeEvidenceRead, laneSummaries.Keys);
        Assert.Contains(ProcessDriverCapabilityScopeKind.BusinessAnalysisRead, laneSummaries.Keys);
        Assert.Contains(ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead, laneSummaries.Keys);
        Assert.Equal(1, laneSummaries[ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead].DeniedCount);
        Assert.Equal(1, laneSummaries[ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead].ErrorCount);
        Assert.Equal(1, laneSummaries[ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead].RedactedResponseCount);
        Assert.True(laneSummaries.Values.All(summary => summary.AllResponsesMutationFree));
        Assert.Contains(
            ProcessDriverDiagnosticCategory.BusinessEvidenceGap,
            laneSummaries[ProcessDriverCapabilityScopeKind.BusinessAnalysisRead].DiagnosticCategories);
        Assert.Contains(
            ProcessDriverDiagnosticCategory.ArtifactLineageMissing,
            laneSummaries[ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead].DiagnosticCategories);
    }

    [Fact]
    public void Process_driver_observation_aggregation_rejects_empty_auditless_and_mixed_lane_responses()
    {
        var aggregator = new ProcessDriverObservationAggregator();
        var auditless = CreateResponse(
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
            accepted: true,
            ProcessDriverDiagnosticCategory.NoIssueDetected,
            ProcessDriverDiagnosticSeverity.Info,
            auditFacts: []);
        var mixedLane = CreateResponse(
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
            accepted: true,
            ProcessDriverDiagnosticCategory.NoIssueDetected,
            ProcessDriverDiagnosticSeverity.Info,
            auditFacts:
            [
                CreateAuditFact(ProcessDriverCapabilityScopeKind.RuntimeFactsRead, accepted: true),
                CreateAuditFact(ProcessDriverCapabilityScopeKind.BusinessAnalysisRead, accepted: true)
            ]);

        Assert.Throws<ArgumentException>(() => aggregator.Aggregate(new ProcessDriverObservationAggregationRequest(
            [],
            DateTimeOffset.Parse("2026-06-08T15:05:00Z"),
            "manager:observation-empty")));
        Assert.Throws<ArgumentException>(() => aggregator.Aggregate(new ProcessDriverObservationAggregationRequest(
            [auditless],
            DateTimeOffset.Parse("2026-06-08T15:06:00Z"),
            "manager:observation-auditless")));
        Assert.Throws<ArgumentException>(() => aggregator.Aggregate(new ProcessDriverObservationAggregationRequest(
            [mixedLane],
            DateTimeOffset.Parse("2026-06-08T15:07:00Z"),
            "manager:observation-mixed")));
    }

    [Fact]
    public void Process_driver_observation_aggregation_package_is_solution_bound_dependency_clean_and_runtime_free()
    {
        var root = FindRepositoryRoot();
        var solution = ReadRepositoryFile("CanDoItAll.slnx");
        var project = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Drivers.ObservationAggregation",
            "CanDoItAll.Processes.Drivers.ObservationAggregation.csproj");
        var source = ReadProjectSource(root);

        Assert.Contains(
            "src/CanDoItAll.Processes.Drivers.ObservationAggregation/CanDoItAll.Processes.Drivers.ObservationAggregation.csproj",
            solution,
            StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.Processes.Drivers.Abstractions.csproj", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", project, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Drivers.TranscriptVerification", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Drivers.RuntimeEvidence", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Drivers.OfficeEvidence", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Drivers.BusinessAnalysis", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Drivers.ArtifactEvidence", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Infrastructure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.AgentFramework", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceCollection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddScoped", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddSingleton", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Diagnostics.Process", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverRuntimeSelector", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverManagerCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverHost", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriverProvider", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new TranscriptVerificationAlphaVerifier", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new RuntimeEvidenceAlphaVerifier", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OfficeEvidenceAlphaVerifier", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new BusinessAnalysisAlphaVerifier", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ArtifactEvidenceAlphaVerifier", source, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverVerificationResponse", source, StringComparison.Ordinal);
        Assert.Contains("AuditFacts", source, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverCapabilityScopeKind", source, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverObservationAggregator", source, StringComparison.Ordinal);
        Assert.Contains("AggregationMutationFree: true", source, StringComparison.Ordinal);
        Assert.Contains("ResolveLane", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_driver_observation_aggregation_returns_readonly_snapshot_envelopes_without_tracking_mutable_inputs()
    {
        var responses = new List<ProcessDriverVerificationResponse>
        {
            CreateResponse(
                ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
                accepted: true,
                ProcessDriverDiagnosticCategory.NoIssueDetected,
                ProcessDriverDiagnosticSeverity.Info),
            CreateResponse(
                ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
                accepted: true,
                ProcessDriverDiagnosticCategory.RuntimeEvidenceInconsistent,
                ProcessDriverDiagnosticSeverity.Warning)
        };
        var aggregator = new ProcessDriverObservationAggregator();
        var result = aggregator.Aggregate(new ProcessDriverObservationAggregationRequest(
            responses,
            DateTimeOffset.Parse("2026-06-08T15:20:00Z"),
            "manager:observation-readonly"));

        responses.Clear();
        responses.Add(CreateResponse(
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
            accepted: false,
            ProcessDriverDiagnosticCategory.ArtifactLineageMissing,
            ProcessDriverDiagnosticSeverity.Error));

        Assert.Equal(2, result.ResponseCount);
        Assert.Equal(2, result.AcceptedCount);
        Assert.Equal(0, result.DeniedCount);
        Assert.Equal(2, result.LaneSummaries.Count);
        Assert.Contains(result.LaneSummaries, summary =>
            summary.Lane == ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification);
        Assert.Contains(result.LaneSummaries, summary =>
            summary.Lane == ProcessDriverCapabilityScopeKind.RuntimeFactsRead);
        Assert.DoesNotContain(result.LaneSummaries, summary =>
            summary.Lane == ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead);
        AssertReadOnlyList(result.LaneSummaries, result.LaneSummaries[0]);
        AssertReadOnlyList(
            result.LaneSummaries[0].DiagnosticCategories,
            ProcessDriverDiagnosticCategory.NoIssueDetected);
        AssertReadOnlyList(result.EvidenceReferences, result.EvidenceReferences[0]);
        AssertReadOnlyList(result.Redaction.AppliedKinds, ProcessDriverRedactionKind.Secret);
    }

    [Fact]
    public void Process_driver_observation_aggregation_remains_unregistered_unpersisted_unscheduled_and_command_free()
    {
        var root = FindRepositoryRoot();
        var packageSource = ReadProjectSource(root);
        var productionSourceOutsidePackage = ReadProductionSourceOutsideAggregation(
            root,
            CreateApprovedSourceConsumers());
        var productionProjectsOutsidePackage = ReadProductionProjectsOutsideAggregation(
            root,
            CreateApprovedProjectConsumers());

        Assert.DoesNotContain(
            "CanDoItAll.Processes.Drivers.ObservationAggregation",
            productionSourceOutsidePackage,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ProcessDriverObservationAggregator",
            productionSourceOutsidePackage,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ProcessDriverObservationAggregationRequest",
            productionSourceOutsidePackage,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ProcessDriverObservationAggregate",
            productionSourceOutsidePackage,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CanDoItAll.Processes.Drivers.ObservationAggregation.csproj",
            productionProjectsOutsidePackage,
            StringComparison.Ordinal);

        foreach (var forbiddenToken in new[]
        {
            "IServiceCollection",
            "ServiceCollection",
            "AddTransient",
            "AddScoped",
            "AddSingleton",
            "IHostedService",
            "BackgroundService",
            "PeriodicTimer",
            "Channel<",
            "DbContext",
            "DbSet<",
            "EntityTypeBuilder",
            "MigrationBuilder",
            "SaveChanges",
            "ExecuteSql",
            "ProcessDriverManagerCommand",
            "ICommand",
            "CommandHandler",
            "Scheduler",
            "Schedule",
            "Hangfire",
            "Quartz",
            "MediatR",
            "File.",
            "Directory.",
            "HttpClient",
            "Process.Start"
        })
        {
            Assert.DoesNotContain(forbiddenToken, packageSource, StringComparison.Ordinal);
        }

        Assert.Contains("CreateReadonlyList", packageSource, StringComparison.Ordinal);
        Assert.Contains("Array.AsReadOnly", packageSource, StringComparison.Ordinal);
        AssertApprovedConsumerReferences(root);
    }

    private static ProcessDriverVerificationResponse CreateResponse(
        ProcessDriverCapabilityScopeKind lane,
        bool accepted,
        ProcessDriverDiagnosticCategory category,
        ProcessDriverDiagnosticSeverity severity,
        ProcessDriverRedactionDescriptor? redaction = null,
        bool noMutation = true,
        string? diagnosticMessage = null,
        IReadOnlyList<ProcessDriverAuditFact>? auditFacts = null)
    {
        var evidence = CreateEvidenceReference(lane);
        var denialReason = accepted
            ? ProcessDriverDenialReason.None
            : ProcessDriverDenialReason.MissingEvidence;

        return new ProcessDriverVerificationResponse(
            accepted,
            denialReason,
            [new ProcessDriverDiagnostic(
                severity,
                category,
                diagnosticMessage ?? $"{lane} observation",
                evidence)],
            [evidence],
            redaction ?? NoRedaction,
            noMutation,
            auditFacts ?? [CreateAuditFact(lane, accepted, redaction, evidence)],
            ProcessDriverContractVersion.Current);
    }

    private static ProcessDriverAuditFact CreateAuditFact(
        ProcessDriverCapabilityScopeKind lane,
        bool accepted,
        ProcessDriverRedactionDescriptor? redaction = null,
        ProcessDriverEvidenceReference? evidenceReference = null)
    {
        var evidence = evidenceReference ?? CreateEvidenceReference(lane);
        var denialReason = accepted
            ? ProcessDriverDenialReason.None
            : ProcessDriverDenialReason.MissingEvidence;

        return new ProcessDriverAuditFact(
            CreateGuid((int)lane),
            DateTimeOffset.Parse("2026-06-08T15:00:00Z"),
            accepted ? ProcessDriverAuditFactKind.DiagnosticReturned : ProcessDriverAuditFactKind.OperationDenied,
            "manager:observation-aggregate",
            CreatePermissionMode(lane),
            CreateScope(lane),
            lane,
            CreateOperation(lane),
            [evidence],
            denialReason,
            redaction ?? NoRedaction,
            $"{lane} redacted diagnostic summary",
            ProcessDriverEvidencePolicy.ComputeSha256($"{lane} redacted diagnostic summary"));
    }

    private static ProcessDriverEvidenceReference CreateEvidenceReference(ProcessDriverCapabilityScopeKind lane)
    {
        var (kind, family, uri) = lane switch
        {
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification => (
                ProcessDriverEvidenceReferenceKind.CommandTranscript,
                ProcessDriverCoreDescriptorFamily.ExecutionEvidence,
                "artifact://proof/scenario037/transcript-observation.txt"),
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead => (
                ProcessDriverEvidenceReferenceKind.CoreDescriptor,
                ProcessDriverCoreDescriptorFamily.ExecutionEvidence,
                "artifact://proof/scenario037/runtime-observation.json"),
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead => (
                ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact,
                (ProcessDriverCoreDescriptorFamily?)null,
                "artifact://proof/scenario037/office-observation.json"),
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead => (
                ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact,
                (ProcessDriverCoreDescriptorFamily?)null,
                "artifact://proof/scenario037/business-observation.json"),
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead => (
                ProcessDriverEvidenceReferenceKind.CoreDescriptor,
                ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence,
                "artifact://proof/scenario037/artifact-observation.json"),
            _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unsupported aggregation lane.")
        };

        return new ProcessDriverEvidenceReference(
            kind,
            uri,
            ProcessDriverEvidencePolicy.ComputeSha256($"{lane}:{uri}"),
            family);
    }

    private static ProcessDriverCapabilityScope CreateScope(ProcessDriverCapabilityScopeKind lane)
    {
        return new ProcessDriverCapabilityScope(
            lane,
            CreatePermissionMode(lane),
            AllowsProcessMutation: false,
            AllowsExternalCalls: false,
            AllowsWorkspaceWrites: false,
            AllowsStorageWrites: false);
    }

    private static ProcessDriverPermissionMode CreatePermissionMode(ProcessDriverCapabilityScopeKind lane)
    {
        return lane == ProcessDriverCapabilityScopeKind.RuntimeFactsRead
            ? ProcessDriverPermissionMode.ManagerReadonly
            : ProcessDriverPermissionMode.VerificationOnly;
    }

    private static ProcessDriverOperation CreateOperation(ProcessDriverCapabilityScopeKind lane)
    {
        return lane == ProcessDriverCapabilityScopeKind.RuntimeFactsRead
            ? ProcessDriverOperation.ReadProcessFacts
            : ProcessDriverOperation.InspectExistingEvidence;
    }

    private static ProcessDriverRedactionDescriptor NoRedaction { get; } = new(
        ProcessDriverRedactionStatus.None,
        [],
        ProcessDriverEvidencePolicy.ComputeSha256(string.Empty));

    private static Guid CreateGuid(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private static void AssertReadOnlyList<T>(IReadOnlyList<T> values, T sample)
    {
        Assert.False(values.GetType().IsArray);

        var collection = Assert.IsAssignableFrom<ICollection<T>>(values);

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Add(sample));
    }

    private static string ReadProjectSource(string repositoryRoot)
    {
        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(
                    Path.Combine(repositoryRoot, "src", "CanDoItAll.Processes.Drivers.ObservationAggregation"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadProductionSourceOutsideAggregation(
        string repositoryRoot,
        IReadOnlySet<string> approvedConsumerFileNames)
    {
        var packageRoot = Path.GetFullPath(Path.Combine(
            repositoryRoot,
            "src",
            "CanDoItAll.Processes.Drivers.ObservationAggregation"));

        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(path => IsRepositorySourceFileOutsidePackage(path, packageRoot) &&
                    !approvedConsumerFileNames.Contains(Path.GetFileName(path)))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadProductionProjectsOutsideAggregation(
        string repositoryRoot,
        IReadOnlySet<string> approvedProjectFileNames)
    {
        var packageRoot = Path.GetFullPath(Path.Combine(
            repositoryRoot,
            "src",
            "CanDoItAll.Processes.Drivers.ObservationAggregation"));

        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
                .Where(path => IsRepositorySourceFileOutsidePackage(path, packageRoot) &&
                    !approvedProjectFileNames.Contains(Path.GetFileName(path)))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static HashSet<string> CreateApprovedSourceConsumers()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ProcessDriverVerificationBatch.cs",
            "ProcessDriverVerificationGateway.cs",
            "ProcessDomainEvidenceReadOnlyAdapters.cs",
            "ProcessDriverObservationAggregationReadOnlyAdapter.cs",
            "ProcessManagerReadOnlyVerificationProjection.cs",
            "ProcessReadOnlyVerificationAggregateObservation.cs"
        };
    }

    private static HashSet<string> CreateApprovedProjectConsumers()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CanDoItAll.Modules.Processes.csproj",
            "CanDoItAll.Processes.Drivers.VerificationGateway.csproj"
        };
    }

    private static void AssertApprovedConsumerReferences(string repositoryRoot)
    {
        var gatewaySource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "CanDoItAll.Processes.Drivers.VerificationGateway",
            "ProcessDriverVerificationGateway.cs"));
        var adapterSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessDriverObservationAggregationReadOnlyAdapter.cs"));

        Assert.Contains("ProcessDriverObservationAggregator", gatewaySource, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverObservationAggregationRequest", gatewaySource, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverVerificationGateway.CreateDefault().AggregateObservations", adapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new ProcessDriverObservationAggregator", adapterSource, StringComparison.Ordinal);
        Assert.Contains("ProcessDriverObservationAggregationRequest", adapterSource, StringComparison.Ordinal);
    }

    private static bool IsRepositorySourceFileOutsidePackage(string path, string packageRoot)
    {
        var fullPath = Path.GetFullPath(path);

        return !fullPath.StartsWith(packageRoot, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !fullPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. pathParts]));
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(sourceFilePath) ?? string.Empty })
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                continue;
            }

            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
