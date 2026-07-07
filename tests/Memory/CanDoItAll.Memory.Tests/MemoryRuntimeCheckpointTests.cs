using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests;

public sealed class MemoryRuntimeCheckpointTests
{
    [Fact]
    public void CP001_Generic_runtime_projects_have_no_native_or_qdrant_dependencies()
    {
        var forbiddenPatterns = new[]
        {
            "CanDoItAll.Modules.CognitiveMemory",
            "CognitiveMemory",
            "Qdrant",
            "OpenAI",
            "OpenAi",
            "CanDoItAll.AgentFramework.Rag"
        };
        var violations = EnumerateSourceFiles("src", "Memory")
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = path,
                    LineNumber = index + 1,
                    Line = line
                }))
            .SelectMany(candidate => forbiddenPatterns
                .Where(pattern => candidate.Line.Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"{Path.GetRelativePath(RepoRoot, candidate.Path)}:{candidate.LineNumber} contains {pattern}"))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void CP002_Generic_runtime_has_no_blocking_async_misuse()
    {
        var forbiddenPatterns = new[]
        {
            ".Result",
            ".Wait(",
            "GetAwaiter().GetResult(",
            "Thread.Sleep(",
            "Task.Delay("
        };
        var violations = EnumerateSourceFiles("src", "Memory")
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = path,
                    LineNumber = index + 1,
                    Line = line
                }))
            .SelectMany(candidate => forbiddenPatterns
                .Where(pattern => candidate.Line.Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"{Path.GetRelativePath(RepoRoot, candidate.Path)}:{candidate.LineNumber} contains {pattern}"))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public async Task CP003_Zero_provider_runtime_registration_has_no_implicit_driver_or_worker_dispatch()
    {
        using var rootProvider = CreateServiceProvider();
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var runtime = provider.GetRequiredService<IMemoryRuntimeService>();

        var operationResult = await runtime.ExecuteContextQueryAsync(
            CreateRuntimeRequest(),
            CreateQueryRequest());
        var workerResult = await provider.GetRequiredService<IMemoryAsyncOperationWorker>()
            .PollOperationsAsync();

        Assert.Equal(MemoryProviderSelectionStatus.NoProviderConfigured, operationResult.Selection.Status);
        Assert.False(operationResult.DriverDispatchAttempted);
        Assert.Empty(provider.GetServices<IMemoryProviderDriver>());
        Assert.Empty(provider.GetServices<IMemoryProviderOperationStatusDriver>());
        Assert.Equal(0, workerResult.Scanned);
    }

    [Fact]
    public async Task CP004_Accepted_operation_metadata_is_persisted_with_ledger_transition()
    {
        var driver = new AcceptingMemoryProviderDriver();
        using var rootProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton<IMemoryProviderDriver>(driver);
        });
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        await provider.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(CreateProviderProfile(), DateTimeOffset.UtcNow);

        var runtimeResult = await provider.GetRequiredService<IMemoryRuntimeService>()
            .ExecuteContextQueryAsync(CreateRuntimeRequest(), CreateQueryRequest());
        Assert.NotNull(runtimeResult.OperationRecord);
        var operationId = runtimeResult.OperationRecord.OperationId;
        var persisted = await provider.GetRequiredService<IMemoryOperationLedgerStore>()
            .GetAsync(operationId);
        var accepted = persisted?.Extensions.GetAcceptedOperation();

        Assert.Equal(MemoryLedgerStatus.Running, persisted?.Status);
        Assert.NotNull(accepted);
        Assert.Equal(operationId, accepted.OperationId);
        Assert.Equal($"/memory/operations/{operationId}", accepted.StatusPath);
        Assert.Equal(TimeSpan.FromSeconds(5), accepted.PollAfter);
    }

    private static ServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"memory-runtime-checkpoint-{Guid.NewGuid():N}"));
        configure?.Invoke(services);
        services.AddGenericMemoryModule();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static MemoryRuntimeOperationRequest CreateRuntimeRequest()
    {
        return new MemoryRuntimeOperationRequest(
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQueryAsync),
            MemoryProviderSelectionContext.None,
            MemoryOperationKind.ContextQuery,
            CreateRequester(),
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            [MemorySourceSnapshotId.Parse("snapshot.project.1")],
            MemoryLedgerRetentionPolicy.Expiring(
                DateTimeOffset.UtcNow.AddDays(7),
                DateTimeOffset.UtcNow.AddDays(30)));
    }

    private static MemoryContextQueryRequest CreateQueryRequest()
    {
        return new MemoryContextQueryRequest(
            "payment integration",
            [MemoryCapabilityIds.ContextQueryAsync],
            new MemorySourceProvenance(
                MemorySourceSnapshotId.Parse("snapshot.project.1"),
                SourceModule: nameof(MemorySourceKind.Project),
                SourceRecordIds: ["project-1"],
                Citations: ["Project 1"]));
    }

    private static MemoryProviderProfile CreateProviderProfile()
    {
        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse("provider.accepting"),
            DisplayName: "Accepting memory provider",
            MemoryProviderDriverKind.Mock,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: ["checkpoint"],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.mock"),
                MemoryProtocolVersion.Current,
                [new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQueryAsync, "1", Supported: true)],
                new MemoryProviderInteractionSupport(
                    SupportsSynchronousQueries: false,
                    SupportsAsynchronousOperations: true,
                    SupportsSourceRequests: false,
                    SupportsFeedback: false,
                    SupportsProviderEvents: false),
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.Empty));
    }

    private static MemoryLedgerRequester CreateRequester()
    {
        return new MemoryLedgerRequester(
            RequesterId: "user-42",
            AgentId: "agent-dev",
            AgentRole: "developer",
            SessionId: "session-7",
            WorkflowId: null,
            WorkflowNodeId: null,
            ProcessId: null,
            ProcessStepId: null);
    }

    private static IEnumerable<string> EnumerateSourceFiles(params string[] segments)
    {
        var root = Path.Combine(new[] { RepoRoot }.Concat(segments).ToArray());
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static string RepoRoot => FindRepoRoot();

    private static string FindRepoRoot()
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

        throw new DirectoryNotFoundException("Could not locate repository root containing CanDoItAll.slnx.");
    }

    private sealed class AcceptingMemoryProviderDriver : IMemoryProviderDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
            MemoryProviderProfile provider,
            MemoryOperationRecord operation,
            MemoryContextQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            var accepted = new MemoryOperationAccepted(
                operation.OperationId,
                $"/memory/operations/{operation.OperationId}",
                DateTimeOffset.UtcNow.AddMinutes(2),
                TimeSpan.FromSeconds(5),
                CallbackAvailable: false);
            return Task.FromResult(MemoryProviderDriverResult.Accepted(accepted, "provider accepted"));
        }
    }
}
