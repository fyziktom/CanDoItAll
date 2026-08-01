using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentExecutionOperationIdentityTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void Execution_run_json_round_trip_preserves_initial_activity_operation_id()
    {
        var operationId = AgentExecutionOperationId.New();
        var run = CreateRun() with
        {
            InitialActivityOperationId = operationId
        };

        var json = JsonSerializer.Serialize(run, SerializerOptions);
        var restored = Assert.IsType<ExecutionRunRecord>(
            JsonSerializer.Deserialize<ExecutionRunRecord>(
                json,
                SerializerOptions));

        Assert.True(restored.InitialActivityOperationId.HasValue);
        Assert.Equal(
            operationId,
            restored.InitialActivityOperationId.Value);
    }

    [Fact]
    public void Legacy_execution_run_json_without_activity_operation_id_deserializes_null()
    {
        var legacyJson = JsonSerializer.Serialize(
            CreateRun(),
            SerializerOptions);

        Assert.DoesNotContain(
            "initialActivityOperationId",
            legacyJson,
            StringComparison.OrdinalIgnoreCase);

        var restored = Assert.IsType<ExecutionRunRecord>(
            JsonSerializer.Deserialize<ExecutionRunRecord>(
                legacyJson,
                SerializerOptions));

        Assert.Null(restored.InitialActivityOperationId);
    }

    [Fact]
    public void Execution_run_json_round_trip_preserves_process_dispatch_claim_identity()
    {
        var dispatchClaimIdentity = new ProcessDispatchClaimIdentity(Guid.NewGuid());
        var run = CreateRun() with
        {
            MetadataJson = JsonSerializer.Serialize(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessDispatchClaimExecutionMetadata.MetadataKey] =
                        dispatchClaimIdentity.Value.ToString("D")
                },
                SerializerOptions)
        };

        var json = JsonSerializer.Serialize(run, SerializerOptions);
        var restored = Assert.IsType<ExecutionRunRecord>(
            JsonSerializer.Deserialize<ExecutionRunRecord>(
                json,
                SerializerOptions));

        Assert.True(ProcessDispatchClaimExecutionMetadata.Matches(
            restored,
            dispatchClaimIdentity));
    }

    [Fact]
    public async Task Execute_run_rejects_default_activity_operation_id_before_dependency_access()
    {
        using var context = CreateServiceContext();
        var request = new ExecutionRunRequest(
            Guid.NewGuid(),
            "Explain the current state.",
            default);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => context.Service.ExecuteRunAsync(request));

        Assert.Equal("request", exception.ParamName);
        AssertDependenciesWereNotAccessed(context);
    }

    [Fact]
    public async Task Continuation_rejects_default_activity_operation_id_before_dependency_access()
    {
        using var context = CreateServiceContext();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => context.Service.ContinueExecutionRunAsync(
                Guid.NewGuid(),
                default,
                approved: true));

        Assert.Equal("activityOperationId", exception.ParamName);
        AssertDependenciesWereNotAccessed(context);
    }

    [Fact]
    public void Runtime_execution_options_preserve_explicit_continuation_activity_operation_id()
    {
        var activityOperationId = AgentExecutionOperationId.New();
        var method = typeof(AgentFrameworkWorkspaceExecutionService).GetMethod(
            "BuildRuntimeExecutionOptions",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "BuildRuntimeExecutionOptions method was not found.");

        var options = Assert.IsType<AgentRuntimeExecutionOptions>(
            method.Invoke(
                null,
                [
                    CreateRun(),
                    activityOperationId,
                    null,
                    null,
                    Array.Empty<AgentRuntimeInputAttachment>(),
                    null,
                    null
                ]));

        Assert.Equal(activityOperationId, options.ActivityOperationId);
    }

    private static ExecutionRunRecord CreateRun()
    {
        var now = new DateTimeOffset(
            2026,
            7,
            27,
            12,
            0,
            0,
            TimeSpan.Zero);
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Operation identity test",
            SourceKind: "unit-test",
            SourceId: "operation-identity",
            CorrelationId: "correlation",
            CausationId: string.Empty,
            RequestedBy: "unit-test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: "Explain the current state.",
            ResultSummary: string.Empty,
            ProviderName: "test-provider",
            Model: "test-model",
            State: ExecutionState.Preparing,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: null,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private static UnexpectedDependency<T> CreateUnexpectedDependency<T>()
        where T : class
    {
        var service = DispatchProxy.Create<T, UnexpectedCallProxy>();
        return new UnexpectedDependency<T>(
            service,
            (UnexpectedCallProxy)(object)service);
    }

    private static ServiceContext CreateServiceContext()
    {
        var store = CreateUnexpectedDependency<ISandboxWorkspaceStore>();
        var packageService = CreateUnexpectedDependency<IAgentPackageService>();
        var runtime = CreateUnexpectedDependency<IAgentRuntime>();
        var capabilityProofService =
            CreateUnexpectedDependency<ICapabilityProofService>();
        var activityCoordinator = new AgentExecutionActivityCoordinator(
            new PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default,
                TimeProvider.System),
            TimeProvider.System);
        var preparationCache = new AgentExecutionPreparationCache(
            AgentExecutionPreparationCachePolicy.Default);
        var service = new AgentFrameworkWorkspaceService(
            store.Service,
            packageService.Service,
            runtime.Service,
            capabilityProofService.Service,
            NullLogger<AgentFrameworkWorkspaceService>.Instance,
            activityCoordinator,
            AgentExecutionActivityWorkspaceIdentity.CreateHostLifetime(
                WorkspaceScopeDescriptor.Sandbox),
            preparationCache,
            new FixedAgentExecutionProfileGenerationSource(default),
            SuccessfulWorkspaceExecutionRunProcessLeaseCleaner.Instance);
        return new ServiceContext(
            service,
            preparationCache,
            store.Proxy,
            packageService.Proxy,
            runtime.Proxy,
            capabilityProofService.Proxy);
    }

    private static void AssertDependenciesWereNotAccessed(ServiceContext context)
    {
        Assert.Equal(0, context.Store.CallCount);
        Assert.Equal(0, context.PackageService.CallCount);
        Assert.Equal(0, context.Runtime.CallCount);
        Assert.Equal(0, context.CapabilityProofService.CallCount);
    }

    private sealed record ServiceContext(
        AgentFrameworkWorkspaceService Service,
        AgentExecutionPreparationCache PreparationCache,
        UnexpectedCallProxy Store,
        UnexpectedCallProxy PackageService,
        UnexpectedCallProxy Runtime,
        UnexpectedCallProxy CapabilityProofService) : IDisposable
    {
        public void Dispose()
        {
            Service.Dispose();
            PreparationCache.Dispose();
        }
    }

    private sealed record UnexpectedDependency<T>(
        T Service,
        UnexpectedCallProxy Proxy)
        where T : class;

    private class UnexpectedCallProxy : DispatchProxy
    {
        private int callCount;

        public int CallCount => Volatile.Read(ref callCount);

        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            Interlocked.Increment(ref callCount);
            throw new InvalidOperationException(
                $"Dependency member '{targetMethod?.Name}' was not expected.");
        }
    }
}
