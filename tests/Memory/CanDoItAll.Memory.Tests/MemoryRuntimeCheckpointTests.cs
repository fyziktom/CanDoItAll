using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests.Runtime;

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
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}Drivers{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
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
        Assert.Equal(operationId.Value.ToString("D"), accepted.StatusPath);
        Assert.Equal(TimeSpan.FromSeconds(5), accepted.PollAfter);
    }

    [Fact]
    public async Task CP005_Mismatched_accepted_operation_id_fails_the_host_operation()
    {
        using var rootProvider = CreateServiceProvider(services =>
            services.AddSingleton<IMemoryProviderDriver>(
                new AcceptingMemoryProviderDriver(AcceptanceMode.MismatchedOperation)));
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        await provider.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(CreateProviderProfile(), DateTimeOffset.UtcNow);

        var result = await provider.GetRequiredService<IMemoryRuntimeService>()
            .ExecuteContextQueryAsync(CreateRuntimeRequest(), CreateQueryRequest());

        Assert.Equal(MemoryLedgerStatus.Failed, result.OperationRecord?.Status);
        Assert.Null(result.AcceptedOperation);
        Assert.Contains("different operation id", result.Diagnostic, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AcceptanceMode.NonPositivePoll)]
    [InlineData(AcceptanceMode.Expired)]
    [InlineData(AcceptanceMode.StatusPathWithUserInfo)]
    public async Task CP006_Invalid_accepted_operation_schedule_fails_closed(AcceptanceMode mode)
    {
        using var rootProvider = CreateServiceProvider(services =>
            services.AddSingleton<IMemoryProviderDriver>(new AcceptingMemoryProviderDriver(mode)));
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        await provider.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(CreateProviderProfile(), DateTimeOffset.UtcNow);

        var result = await provider.GetRequiredService<IMemoryRuntimeService>()
            .ExecuteContextQueryAsync(CreateRuntimeRequest(), CreateQueryRequest());

        Assert.Equal(MemoryLedgerStatus.Failed, result.OperationRecord?.Status);
        Assert.Null(result.AcceptedOperation);
        Assert.True(result.DriverDispatchAttempted);
    }

    [Fact]
    public async Task CP007_Cancellation_reports_local_tracking_semantics()
    {
        using var rootProvider = CreateServiceProvider(services =>
            services.AddSingleton<IMemoryProviderDriver>(new AcceptingMemoryProviderDriver()));
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var profile = CreateProviderProfile();
        await provider.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(profile, DateTimeOffset.UtcNow);
        var runtimeResult = await provider.GetRequiredService<IMemoryRuntimeService>()
            .ExecuteContextQueryAsync(CreateRuntimeRequest(), CreateQueryRequest());
        var operationId = Assert.IsType<MemoryOperationRecord>(runtimeResult.OperationRecord).OperationId;

        var result = await provider.GetRequiredService<IMemoryOperationHandler>()
            .CancelAsync(MemoryOperationRequestBuilder.Cancellation(
                MemoryOperationCaller.Tool("memory.cancel", CreateRequester()),
                MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.OperationStatus) with
                {
                    ExplicitProviderId = profile.InstanceId
                },
                new MemoryOperationCancellationRequest(operationId, "stop tracking"),
                MemoryLedgerRetentionPolicy.Expiring(
                    DateTimeOffset.UtcNow.AddDays(1),
                    DateTimeOffset.UtcNow.AddDays(2))));

        Assert.Equal(MemoryOperationHandlerStatus.Cancelled, result.Status);
        Assert.False(result.DriverDispatchAttempted);
        Assert.Contains("locally", result.Diagnostic, StringComparison.Ordinal);
        Assert.Contains("was not dispatched", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CP008_Duplicate_query_drivers_fail_closed_without_dispatch()
    {
        var first = new AcceptingMemoryProviderDriver();
        var second = new AcceptingMemoryProviderDriver();
        using var rootProvider = CreateServiceProvider(services =>
        {
            services.AddSingleton<IMemoryProviderDriver>(first);
            services.AddSingleton<IMemoryProviderDriver>(second);
        });
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        await provider.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(CreateProviderProfile(), DateTimeOffset.UtcNow);

        var result = await provider.GetRequiredService<IMemoryRuntimeService>()
            .ExecuteContextQueryAsync(CreateRuntimeRequest(), CreateQueryRequest());

        Assert.False(result.DriverDispatchAttempted);
        Assert.Equal(MemoryLedgerStatus.Failed, result.OperationRecord?.Status);
        Assert.Equal(0, first.DispatchCount);
        Assert.Equal(0, second.DispatchCount);
        Assert.Contains("Multiple", result.Diagnostic, StringComparison.Ordinal);
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
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQueryAsync) with
            {
                ExplicitProviderId = MemoryProviderInstanceId.Parse("provider.accepting")
            },
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
                [
                    new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQueryAsync, "1", Supported: true),
                    new MemoryCapabilityDescriptor(MemoryCapabilityIds.OperationStatus, "1", Supported: true)
                ],
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

    public enum AcceptanceMode
    {
        Valid = 0,
        MismatchedOperation = 1,
        NonPositivePoll = 2,
        Expired = 3,
        StatusPathWithUserInfo = 4
    }

    private sealed class AcceptingMemoryProviderDriver(
        AcceptanceMode mode = AcceptanceMode.Valid) : IMemoryProviderDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public int DispatchCount { get; private set; }

        public Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
            MemoryProviderProfile provider,
            MemoryOperationRecord operation,
            MemoryContextQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            DispatchCount++;
            var accepted = new MemoryOperationAccepted(
                mode == AcceptanceMode.MismatchedOperation ? MemoryOperationId.New() : operation.OperationId,
                mode == AcceptanceMode.StatusPathWithUserInfo
                    ? "https://agent:secret@memory.example/operations/status"
                    : $"/memory/operations/{operation.OperationId}",
                mode == AcceptanceMode.Expired
                    ? DateTimeOffset.UtcNow.AddMinutes(-1)
                    : DateTimeOffset.UtcNow.AddMinutes(2),
                mode == AcceptanceMode.NonPositivePoll ? TimeSpan.Zero : TimeSpan.FromSeconds(5),
                CallbackAvailable: false);
            return Task.FromResult(MemoryProviderDriverResult.Accepted(accepted, "provider accepted"));
        }
    }
}
