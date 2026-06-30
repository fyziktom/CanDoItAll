using CanDoItAll.Git;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessExecutionAdapterBoundaryTests
{
    [Fact]
    public async Task Adapter_strategy_normalizes_restricted_diagnostics_into_result_envelope()
    {
        var adapter = new RecordingExecutionAdapter(
            StandardProcessAdapterDescriptors.WorkflowAdapter,
            new ProcessExecutionAdapterResult(
                StrategyOutcome.NeedsManager,
                [],
                [],
                [
                    new ProcessExecutionAdapterDiagnostic(
                        new StrategyDiagnosticCode("workflow.raw-transcript"),
                        StrategyDiagnosticSensitivity.Restricted,
                        "sha256:raw",
                        "Workflow failed with a retriable infrastructure error.",
                        "restricted://workflow/run-1",
                        ProcessDiagnosticRetrySafety.SafeToRetry,
                        ProcessDiagnosticIdempotencyClassification.Idempotent)
                ],
                [new ManagerSignal(new ManagerSignalCode("manager.review"), "sha256:signal", "Manager review required.")],
                "Workflow adapter completed with a restricted diagnostic.",
                "sha256:result"));
        var package = StandardProcessAdapterDriverPackageFactory.CreateAdapterPackage(adapter, ProcessDriverLayer.Platform);
        var factory = Assert.Single(package.StrategyFactories);
        var binding = NewBinding(package.Descriptor.DriverId, factory.Descriptor);

        var strategy = await factory.CreateAsync(binding);
        var result = await strategy.ExecuteAsync(new ProcessStrategyExecutionContext(
            ProcessRunId.New(),
            ProcessStepInstanceId.New(),
            binding,
            binding.Inputs));

        var request = Assert.Single(adapter.Requests);
        Assert.Equal(ProcessExecutionAdapterKind.Workflow, request.Kind);
        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(StrategyDiagnosticSensitivity.Restricted, diagnostic.Sensitivity);
        Assert.Equal("restricted://workflow/run-1", diagnostic.RestrictedEvidenceReference);
        Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
        Assert.Equal("sha256:result", result.ResultHash);
    }

    [Fact]
    public void Layered_driver_slice_orders_foundation_before_adapter_driver()
    {
        var adapter = new RecordingExecutionAdapter(
            StandardProcessAdapterDescriptors.WorkflowAdapter,
            ProcessExecutionAdapterResult.Succeeded("workflow completed", "sha256:workflow"));
        var packages = StandardProcessAdapterDriverPackageFactory.CreateLayeredPackages(adapter);
        var catalog = new ProcessDriverCatalog(packages);

        var result = catalog.Match(new ProcessCapabilityRequest(
            new HashSet<CapabilityTag> { StandardProcessAdapterCapabilities.WorkflowExecution },
            new HashSet<CapabilityTag>(),
            new HashSet<CapabilityTag>()));

        Assert.True(result.Succeeded, string.Join(", ", result.Diagnostics));
        Assert.Equal(
            [
                StandardProcessAdapterDriverIds.Foundation.Value,
                StandardProcessAdapterDriverIds.Workflow.Value
            ],
            result.OrderedDrivers.Select(driver => driver.DriverId.Value));
    }

    [Fact]
    public async Task Mutation_audit_reports_unauthorized_paths_using_git_wrapper()
    {
        var executor = new RecordingGitCommandExecutor(
            new GitCommandResult(true, 0, " M src/Allowed/file.cs\n M secrets.txt\n", string.Empty, "git status --short"),
            new GitCommandResult(true, 0, "diff --git a/secrets.txt b/secrets.txt\n+secret\n", string.Empty, "git diff --"));
        var client = new GitRepositoryClient(new GitRepositoryPath(FindRepositoryRoot()), executor);
        var service = new ProcessAdapterMutationAuditService();

        var report = await service.AuditAsync(
            client,
            new ProcessAdapterMutationAuditRequest(
                ProcessRunId.New(),
                ProcessStepInstanceId.New(),
                [new ProcessAdapterMutationScope("src/Allowed")],
                [new ProcessAdapterMutationScope("src/Forbidden")],
                string.Empty),
            CancellationToken.None);

        Assert.Equal(ProcessAdapterMutationAuditOutcome.UnauthorizedPathMutation, report.Outcome);
        Assert.Contains(report.Findings, finding => finding.RepositoryRelativePath == "secrets.txt");
        Assert.StartsWith("sha256:", report.RestrictedDiffReference, StringComparison.Ordinal);
        Assert.Equal(["git status --short", "git diff --"], executor.SanitizedCommands);
    }

    [Fact]
    public void Core_and_runtime_do_not_reference_concrete_adapter_apis()
    {
        var root = FindRepositoryRoot();
        var blockedTerms = new[]
        {
            "CanDoItAll.Processes.Drivers.Standard",
            nameof(IProcessExecutionAdapter),
            nameof(ProcessExecutionAdapterRequest),
            "ProcessExecutionAdapterKind."
        };

        var findings = new[]
        {
            Path.Combine(root, "src", "Processes", "CanDoItAll.Processes.Core"),
            Path.Combine(root, "src", "Processes", "CanDoItAll.Processes.Runtime")
        }
        .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        .SelectMany(path => FindTermMatches(root, path, blockedTerms))
        .ToArray();

        Assert.True(
            findings.Length == 0,
            "Core/Runtime must not reference concrete adapter APIs: " + string.Join(", ", findings));
    }

    private static ProcessStrategyBindingSnapshot NewBinding(
        DriverId driverId,
        ProcessStrategyDescriptor descriptor)
    {
        return new ProcessStrategyBindingSnapshot(
            driverId,
            descriptor.StrategyId,
            descriptor.StrategyVersion,
            "factory/1.0",
            "runtime/1.0",
            "runtime/2.x",
            "sha256:binding",
            [new StrategyBindingInput(new StrategyBindingInputKey("operation"), "sha256:operation")]);
    }

    private static IEnumerable<string> FindTermMatches(
        string root,
        string path,
        IReadOnlyList<string> terms)
    {
        var text = File.ReadAllText(path);
        foreach (var term in terms)
        {
            if (text.Contains(term, StringComparison.Ordinal))
            {
                yield return $"{Path.GetRelativePath(root, path)} contains {term}";
            }
        }
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

    private sealed class RecordingExecutionAdapter(
        ProcessExecutionAdapterDescriptor descriptor,
        ProcessExecutionAdapterResult result) : IProcessExecutionAdapter
    {
        public ProcessExecutionAdapterDescriptor Descriptor { get; } = descriptor;

        public List<ProcessExecutionAdapterRequest> Requests { get; } = [];

        public ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(
            ProcessExecutionAdapterRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecordingGitCommandExecutor(params GitCommandResult[] results) : IGitCommandExecutor
    {
        private readonly Queue<GitCommandResult> queuedResults = new(results);

        public List<string> SanitizedCommands { get; } = [];

        public Task<GitCommandResult> ExecuteAsync(
            GitCommandSpec spec,
            CancellationToken cancellationToken = default)
        {
            SanitizedCommands.Add(spec.SanitizedCommand);
            return Task.FromResult(queuedResults.Dequeue());
        }
    }
}
