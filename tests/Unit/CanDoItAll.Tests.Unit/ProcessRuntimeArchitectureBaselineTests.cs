using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimeArchitectureBaselineTests
{
    private static readonly string[] GenericDomainLeakTerms =
    [
        "IDotNetSolutionSetupRuntimeExecutor",
        "TryExecuteRuntimeOwnedDotNetSetupAsync",
        "IsDotNetRuntimeLifecycleTool",
        "Tetris",
        "qa-validation",
        "quality-accepted",
        "repair-required",
        "repair-escalation",
        "create-dotnet-project",
        "add-test-project",
        "repair-solution-setup",
        "workspace_dotnet_",
        "An unhandled error has occurred.",
        "DotNetSolutionSetup",
        "dotnet-development-slice",
        "software-delivery"
    ];

    [Fact]
    public void AgentFrameworkProcessExecutionAdapter_is_one_non_partial_boundary_file()
    {
        var root = FindRepositoryRoot();
        var adapterDirectory = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Processes",
            "Services",
            "RuntimeIntegration");
        var adapterFiles = Directory
            .EnumerateFiles(adapterDirectory, "AgentFrameworkProcessExecutionAdapter*.cs")
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(["AgentFrameworkProcessExecutionAdapter.cs"], adapterFiles);

        var adapterSource = File.ReadAllText(Path.Combine(adapterDirectory, adapterFiles[0]));
        Assert.DoesNotContain("partial class AgentFrameworkProcessExecutionAdapter", adapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteRunAsync", adapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IWorkspaceFileService", adapterSource, StringComparison.Ordinal);
        Assert.Contains("IAgentFrameworkProcessStepExecutor", adapterSource, StringComparison.Ordinal);
        Assert.True(adapterSource.Count(character => character == '\n') < 90, "The adapter boundary must remain small.");
    }

    [Fact]
    public async Task AgentFrameworkProcessExecutionAdapter_delegates_without_runtime_construction()
    {
        var expectedResult = ProcessExecutionAdapterResult.Succeeded("delegated", "sha256:delegated");
        var executor = new RecordingProcessStepExecutor(expectedResult);
        var adapter = new AgentFrameworkProcessExecutionAdapter(executor);
        var request = new ProcessExecutionAdapterRequest(
            ProcessRunId.New(),
            ProcessStepInstanceId.New(),
            ProcessExecutionAdapterKind.Workflow,
            new ProcessExecutionAdapterOperationKey("adapter.workflow.execute"),
            new ProcessStrategyBindingSnapshot(
                new DriverId("test.workflow"),
                new StrategyId("test.execute"),
                "1.0.0",
                "1.0.0",
                "1",
                "1",
                "sha256:binding",
                []),
            [],
            []);
        using var cancellation = new CancellationTokenSource();

        var result = await adapter.ExecuteAsync(request, cancellation.Token);

        Assert.Same(expectedResult, result);
        Assert.Same(request, executor.Request);
        Assert.Equal(cancellation.Token, executor.CancellationToken);
    }

    [Fact]
    public void Extracted_process_runtime_integration_has_no_partial_types_or_global_static_imports()
    {
        var root = FindRepositoryRoot();
        var adapterDirectory = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Processes",
            "Services",
            "RuntimeIntegration");
        var findings = Directory
            .EnumerateFiles(adapterDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Path: path, Source: File.ReadAllText(path)))
            .Where(file =>
                file.Source.Contains("partial class", StringComparison.Ordinal) ||
                file.Source.Contains("partial record", StringComparison.Ordinal) ||
                file.Source.Contains("partial struct", StringComparison.Ordinal) ||
                file.Source.Contains("global using static", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(root, file.Path))
            .ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void Extracted_adapter_responsibilities_do_not_form_a_replacement_monolith()
    {
        var root = FindRepositoryRoot();
        var adapterDirectory = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Processes",
            "Services",
            "RuntimeIntegration");
        var extractedFiles = new[]
        {
            "AgentFrameworkProcessStepExecutor.cs",
            "ProcessBranchOutcomeResolver.cs",
            "ProcessCompletionIssueResultFactory.cs",
            "ProcessCompletionRuleParser.cs",
            "ProcessExecutionResultConverter.cs",
            "ProcessManagedArtifactService.cs",
            "ProcessOutcomeGroundingValidator.cs",
            "ProcessRequiredReceiptMatcher.cs",
            "ProcessRequiredReceiptRetryPolicy.cs",
            "ProcessSubprocessCompletionPolicy.cs",
            "ProcessSubprocessCoordinator.cs"
        };
        var oversizedFiles = extractedFiles
            .Select(fileName => Path.Combine(adapterDirectory, fileName))
            .Where(path => File.ReadLines(path).Count() >= 500)
            .Select(path => $"{Path.GetFileName(path)}: {File.ReadLines(path).Count()} lines")
            .ToArray();

        Assert.Empty(oversizedFiles);
    }

    [Fact]
    public void Generic_process_runtime_domain_term_baseline_has_no_unclassified_hits()
    {
        var root = FindRepositoryRoot();
        var searchedDirectories = new[]
        {
            Path.Combine(root, "src", "Processes"),
            Path.Combine(root, "src", "Modules", "CanDoItAll.Modules.Processes", "Services", "RuntimeIntegration"),
            Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Core", "Workspace", "Commands")
        };

        var findings = searchedDirectories
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .SelectMany(path => FindTermMatches(root, path, GenericDomainLeakTerms))
            .Where(match => !IsAllowedCurrentDomainTermHit(match.RelativePath))
            .Select(match => $"{match.RelativePath} contains {match.Term}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void WorkspaceCommandReceiptWriter_characterizes_dotnet_lifecycle_facts_in_audit_receipt()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.ProcessRuntimeArchitectureBaselineTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        var executionRun = CreateExecutionRun();

        try
        {
            var startedAtUtc = DateTimeOffset.UtcNow;
            var completedAtUtc = startedAtUtc.AddSeconds(2);
            var startupReceiptPath = "artifacts/process-runs/run-001/tool-runs/dotnet-run/startup.json";
            var writer = new WorkspaceCommandReceiptWriter(
                workspaceRoot,
                lifecycleFactExtractors: [new DotNetWorkspaceCommandReceiptLifecycleFactExtractor()]);

            using (WorkspaceExecutionAuditContext.BeginScope(executionRun))
            {
                writer.PersistProcessReceipt(
                    "workspace_dotnet_run",
                    "dotnet-run",
                    new ToolExecutionDecision(
                        "workspace_dotnet_run",
                        "dotnet-run",
                        "WorkspaceProcess",
                        Allowed: true,
                        ApprovalRequired: false,
                        NetworkAllowed: true,
                        ExternalRootsAllowed: false,
                        "Unit test lifecycle characterization."),
                    workingDirectory: ".",
                    arguments: ["run", "src/App/App.csproj"],
                    targetPaths: [startupReceiptPath],
                    mutatesWorkspace: false,
                    message: "Runtime started.",
                    processResult: new WorkspaceProcessExecutionResult(
                        Started: true,
                        ExitCode: 0,
                        Stdout: "Now listening on: http://127.0.0.1:5173",
                        Stderr: string.Empty,
                        StdoutTruncated: false,
                        StderrTruncated: false,
                        StartedAtUtc: startedAtUtc,
                        CompletedAtUtc: completedAtUtc,
                        TimedOut: false,
                        Boundary: new ExecutionBoundaryDescriptor(
                            "Test",
                            "Workspace",
                            "Loopback",
                            "None",
                            "Unit test host",
                            IsEnforcedByHost: false,
                            "Unit test boundary."),
                        FailureMessage: string.Empty));
            }

            var auditReceipt = ReadSingleAuditReceipt(workspaceRoot, executionRun.Id);

            Assert.Equal("workspace_dotnet_run", auditReceipt.ToolName);
            Assert.Contains($"startupReceipt={startupReceiptPath}", auditReceipt.RequestSummary, StringComparison.Ordinal);
            Assert.Contains("hostUrl=http://127.0.0.1:5173", auditReceipt.RequestSummary, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void WorkspaceCommandReceiptWriter_uses_registered_lifecycle_fact_extractor()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.ProcessRuntimeArchitectureBaselineTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        var executionRun = CreateExecutionRun();

        try
        {
            var startedAtUtc = DateTimeOffset.UtcNow;
            var completedAtUtc = startedAtUtc.AddSeconds(1);
            var writer = new WorkspaceCommandReceiptWriter(
                workspaceRoot,
                lifecycleFactExtractors: [new FixedLifecycleFactExtractor("customFact", "custom-value")]);

            using (WorkspaceExecutionAuditContext.BeginScope(executionRun))
            {
                writer.PersistProcessReceipt(
                    "custom_runtime_run",
                    "custom-runtime-run",
                    new ToolExecutionDecision(
                        "custom_runtime_run",
                        "custom-runtime-run",
                        "WorkspaceProcess",
                        Allowed: true,
                        ApprovalRequired: false,
                        NetworkAllowed: false,
                        ExternalRootsAllowed: false,
                        "Unit test lifecycle extractor seam."),
                    workingDirectory: ".",
                    arguments: ["run"],
                    targetPaths: [],
                    mutatesWorkspace: false,
                    message: "Runtime started.",
                    processResult: new WorkspaceProcessExecutionResult(
                        Started: true,
                        ExitCode: 0,
                        Stdout: string.Empty,
                        Stderr: string.Empty,
                        StdoutTruncated: false,
                        StderrTruncated: false,
                        StartedAtUtc: startedAtUtc,
                        CompletedAtUtc: completedAtUtc,
                        TimedOut: false,
                        Boundary: new ExecutionBoundaryDescriptor(
                            "Test",
                            "Workspace",
                            "Loopback",
                            "None",
                            "Unit test host",
                            IsEnforcedByHost: false,
                            "Unit test boundary."),
                        FailureMessage: string.Empty));
            }

            var auditReceipt = ReadSingleAuditReceipt(workspaceRoot, executionRun.Id);

            Assert.Equal("custom_runtime_run", auditReceipt.ToolName);
            Assert.Contains("customFact=custom-value", auditReceipt.RequestSummary, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void WorkspaceCommandReceiptWriter_has_no_dotnet_lifecycle_branching()
    {
        var root = FindRepositoryRoot();
        var writerPath = Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Core",
            "Workspace",
            "Commands",
            "WorkspaceCommandReceiptWriter.cs");
        var writerSource = File.ReadAllText(writerPath);

        Assert.DoesNotContain("workspace_dotnet_run", writerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_dotnet_stop", writerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsDotNetRuntimeLifecycleTool", writerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentFrameworkProcessExecutionAdapter_has_no_direct_dotnet_setup_executor_dependency()
    {
        var root = FindRepositoryRoot();
        var adapterDirectory = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Processes",
            "Services",
            "RuntimeIntegration");
        var adapterSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(adapterDirectory, "AgentFrameworkProcessExecutionAdapter*.cs")
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("IDotNetSolutionSetupRuntimeExecutor", adapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryExecuteRuntimeOwnedDotNetSetupAsync", adapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("dotNetSolutionSetupRuntimeExecutor", adapterSource, StringComparison.Ordinal);
    }

    private static bool IsAllowedCurrentDomainTermHit(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith("src/Processes/CanDoItAll.Processes.Templates/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.StartsWith("src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandPlanBuilder", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandExecutionService", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.StartsWith(
                "src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/Drivers/DotNet/",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalized is
            "src/Processes/CanDoItAll.Processes.Runtime/ProcessRecoveryClassifier.cs" or
            "src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs";
    }

    private static IEnumerable<TermMatch> FindTermMatches(
        string root,
        string path,
        IReadOnlyList<string> terms)
    {
        var text = File.ReadAllText(path);
        foreach (var term in terms)
        {
            if (text.Contains(term, StringComparison.Ordinal))
            {
                yield return new TermMatch(Path.GetRelativePath(root, path), term);
            }
        }
    }

    private static ToolExecutionReceiptRecord ReadSingleAuditReceipt(string workspaceRoot, Guid executionRunId)
    {
        var receiptDirectory = Path.Combine(
            workspaceRoot,
            "data",
            "execution",
            "runs",
            executionRunId.ToString("N"),
            "audit",
            "receipts");
        var receiptPath = Assert.Single(Directory.GetFiles(receiptDirectory, "*.json"));

        return JsonSerializer.Deserialize<ToolExecutionReceiptRecord>(
                   File.ReadAllText(receiptPath),
                   new JsonSerializerOptions(JsonSerializerDefaults.Web))
               ?? throw new InvalidOperationException("Audit receipt JSON did not deserialize.");
    }

    private static ExecutionRunRecord CreateExecutionRun()
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Receipt lifecycle characterization",
            SourceKind: "process-step",
            SourceId: "qa-validation",
            CorrelationId: "run-001",
            CausationId: "step-001",
            RequestedBy: "process-runtime",
            RequestedByKind: "system",
            MetadataJson: "{}",
            InputSummary: "Input",
            ResultSummary: string.Empty,
            ProviderName: "Provider",
            Model: "model",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed record TermMatch(string RelativePath, string Term);

    private sealed class RecordingProcessStepExecutor(ProcessExecutionAdapterResult result) : IAgentFrameworkProcessStepExecutor
    {
        public ProcessExecutionAdapterRequest? Request { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(
            ProcessExecutionAdapterRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FixedLifecycleFactExtractor(string name, string value) : IWorkspaceCommandReceiptLifecycleFactExtractor
    {
        public IReadOnlyList<WorkspaceCommandReceiptLifecycleFact> Extract(
            WorkspaceCommandReceiptLifecycleFactContext context)
            => [new WorkspaceCommandReceiptLifecycleFact(name, value)];
    }
}
