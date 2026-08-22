using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExternalResponseRecoveryWorkerTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        DateTimeOffset.Parse("2026-08-21T12:00:00Z");

    [Fact]
    public void ModuleRegistersRecoveryWorkerOnlyForBackgroundWorkerLane()
    {
        var backgroundServices = new ServiceCollection();
        backgroundServices.AddAgentFrameworkModule(new ConfigurationBuilder().Build());
        var suppressedServices = new ServiceCollection();
        var suppressedConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] =
                    LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
            })
            .Build();
        suppressedServices.AddAgentFrameworkModule(suppressedConfiguration);

        Assert.Contains(
            backgroundServices,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                          descriptor.ImplementationType == typeof(WorkflowExternalResponseRecoveryWorker) &&
                          descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.DoesNotContain(
            suppressedServices,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                          descriptor.ImplementationType == typeof(WorkflowExternalResponseRecoveryWorker));
    }

    [Fact]
    public async Task WorkerCreatesOneScopeRunsBoundedPassAndLogsOnlyCountsAndOutcomes()
    {
        var operations = new[] { CreateOperation(1), CreateOperation(2) };
        var store = new RecordingOperationStore(operations);
        var continuation = new RecordingContinuation();
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowExternalResponseOperationStore>(store);
        services.AddSingleton<IWorkflowExternalResponseContinuation>(continuation);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedUtcNow));
        services.AddScoped<WorkflowExternalResponseRecoveryCoordinator>();
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = new CountingScopeFactory(
            provider.GetRequiredService<IServiceScopeFactory>());
        var logger = new RecordingLogger<WorkflowExternalResponseRecoveryWorker>();
        var worker = new WorkflowExternalResponseRecoveryWorker(scopeFactory, logger);

        await worker.StartAsync(CancellationToken.None);
        await Assert.IsAssignableFrom<Task>(worker.ExecuteTask);

        Assert.Equal(1, scopeFactory.CreateScopeCount);
        Assert.Equal(1, store.ListCalls);
        Assert.Equal(WorkflowExternalResponseRecoveryCoordinator.DefaultMaximumCount, store.MaximumCount);
        Assert.Equal(2, continuation.ContinueCalls);
        var log = string.Join(Environment.NewLine, logger.Messages);
        Assert.Contains("processed 2 operation(s)", log, StringComparison.Ordinal);
        Assert.Contains("Completed: 1 operation(s)", log, StringComparison.Ordinal);
        Assert.Contains("FailedRetryable: 1 operation(s)", log, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive", log, StringComparison.OrdinalIgnoreCase);
    }

    private static WorkflowExternalResponseOperationRecord CreateOperation(int index)
        => new(
            WorkflowExternalResponseOperationId.New(),
            WorkflowExternalRequestId.New(),
            WorkflowRunId.New(),
            WorkflowExternalRequestVersion.Initial,
            new WorkflowExternalResponseIdempotencyKeyHash(new string((char)('0' + index), 64)),
            new WorkflowExternalResponsePayloadHash(new string((char)('2' + index), 64)),
            new WorkflowExternalResponseActorScopeFingerprint(new string((char)('4' + index), 64)),
            new WorkflowExternalResponsePayload("{\"sensitive\":true}"),
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, $"sensitive-user-{index}"),
            new WorkflowLaunchCorrelationId($"recovery-{index}"),
            WorkflowExternalResponseOperationState.Accepted,
            Attempt: 0,
            WorkflowExternalResponseOperationConcurrencyVersion.Initial,
            FixedUtcNow.AddMinutes(-index));

    private sealed class RecordingOperationStore(
        IReadOnlyList<WorkflowExternalResponseOperationRecord> operations) :
        IWorkflowExternalResponseOperationStore
    {
        public int ListCalls { get; private set; }

        public int? MaximumCount { get; private set; }

        public Task<IReadOnlyList<WorkflowExternalResponseOperationRecord>> ListRecoverableAsync(
            DateTimeOffset asOfUtc,
            int maximumCount,
            CancellationToken cancellationToken = default)
        {
            ListCalls++;
            MaximumCount = maximumCount;
            return Task.FromResult(operations);
        }

        public Task<WorkflowExternalResponseOperationCreateResult> CreateOrReplayAsync(
            WorkflowExternalResponseOperationCreateRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationRecord?> GetAsync(
            WorkflowExternalResponseOperationId operationId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationClaimResult> TryClaimAsync(
            WorkflowExternalResponseOperationClaimRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryRenewLeaseAsync(
            WorkflowExternalResponseOperationLeaseRenewalRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryMarkResumingAsync(
            WorkflowExternalResponseOperationMarkResumingRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryCompleteAsync(
            WorkflowExternalResponseOperationCompletionRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryFailAsync(
            WorkflowExternalResponseOperationFailureRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseOperationMutationResult> TryReleaseLeaseAsync(
            WorkflowExternalResponseOperationLeaseReleaseRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingContinuation : IWorkflowExternalResponseContinuation
    {
        public int ContinueCalls { get; private set; }

        public Task<WorkflowExternalResponseContinuationResult> ContinueAsync(
            WorkflowExternalResponseContinuationRequest request,
            CancellationToken cancellationToken = default)
        {
            var outcome = ContinueCalls++ == 0
                ? WorkflowExternalResponseContinuationOutcome.Completed
                : WorkflowExternalResponseContinuationOutcome.FailedRetryable;
            return Task.FromResult(new WorkflowExternalResponseContinuationResult(
                outcome,
                Operation: null,
                Run: null,
                NextRequest: null,
                "Sensitive continuation details."));
        }
    }

    private sealed class CountingScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        public int CreateScopeCount { get; private set; }

        public IServiceScope CreateScope()
        {
            CreateScopeCount++;
            return inner.CreateScope();
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
