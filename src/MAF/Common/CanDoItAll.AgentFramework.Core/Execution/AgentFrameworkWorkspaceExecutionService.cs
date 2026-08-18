using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService(
    ISandboxWorkspaceStore store,
    IAgentExecutionReportReader executionReportReader,
    IAgentExecutionRuntime executionRuntime,
    IAgentContinuationRuntime continuationRuntime,
    IAgentExecutionGovernanceBridge executionGovernanceBridge,
    IAgentExecutionEventSink executionEventSink,
    IAgentExecutionCheckpointBridge executionCheckpointBridge,
    IProviderRuntimeProfileSource providerSource,
    IAgentProviderCredentialResolver providerCredentialResolver,
    IExternalTargetPathRegistryFactory externalTargetPathRegistryFactory,
    ILogger logger,
    AgentExecutionActivityWorkspaceIdentity activityWorkspaceIdentity,
    IAgentExecutionPreparationService executionPreparationService,
    IWorkspaceExecutionRunProcessLeaseCleaner workspaceProcessLeaseCleaner,
    IAgentExecutionCancellationRegistry? executionCancellationRegistry = null,
    IAgentOutputRepairService? outputRepairService = null,
    IWorkspacePathResolutionService? workspacePathResolutionService = null,
    IEnumerable<IAgentExecutionProviderSelectionPolicy>? providerSelectionPolicies = null,
    IEnumerable<IAgentExecutionRunCriticalityPolicy>? runCriticalityPolicies = null) :
    IDisposable
{
    private readonly IAgentOutputRepairService outputRepairService =
        outputRepairService ?? JsonObjectExtractionAgentOutputRepairService.Instance;
    private readonly IAgentExecutionCancellationRegistry executionCancellationRegistry =
        executionCancellationRegistry ?? new AgentExecutionCancellationRegistry();
    // IEnumerable<T> (not IReadOnlyList<T>) so plain reflection-based DI activation (AddScoped<T>() with no
    // factory - the production AddAgentFrameworkCore/AddAgentFrameworkModule registration shape, and every test
    // host that re-registers this type the same way) auto-supplies every registered policy without requiring a
    // separate IReadOnlyList<T> aggregator registration in each composition root.
    private readonly IReadOnlyList<IAgentExecutionProviderSelectionPolicy> providerSelectionPolicies =
        providerSelectionPolicies?.ToList() ?? [];
    private readonly IReadOnlyList<IAgentExecutionRunCriticalityPolicy> runCriticalityPolicies =
        runCriticalityPolicies?.ToList() ?? [];
    private readonly AgentExecutionActivityWorkspaceIdentity activityWorkspaceIdentity =
        activityWorkspaceIdentity
        ?? throw new ArgumentNullException(nameof(activityWorkspaceIdentity));
    private readonly IAgentExecutionPreparationService executionPreparationService =
        executionPreparationService
        ?? throw new ArgumentNullException(nameof(executionPreparationService));
    private readonly AgentProviderCredentialDispatchScopeFactory
        providerCredentialDispatchScopeFactory =
        new(providerCredentialResolver);
    private readonly IWorkspacePathResolutionService? workspacePathResolutionService = workspacePathResolutionService;
    private readonly IExternalTargetPathRegistryFactory externalTargetPathRegistryFactory =
        externalTargetPathRegistryFactory ?? throw new ArgumentNullException(nameof(externalTargetPathRegistryFactory));
    private readonly IWorkspaceExecutionRunProcessLeaseCleaner workspaceProcessLeaseCleaner =
        workspaceProcessLeaseCleaner
        ?? throw new ArgumentNullException(nameof(workspaceProcessLeaseCleaner));
    private readonly ILogger logger = logger;
    private static readonly AgentProviderUsageObservationAssembler UsageObservationAssembler = new();
    private readonly IsolatedCompatibilityEventDispatcher<ExecutionLogEntry> executionUpdatedDispatcher =
        CreateExecutionUpdatedDispatcher(logger);
    private readonly AgentTurnContextLeaseRegistry transientContextRegistry =
        new(onEvicted: eviction => logger.LogWarning(
            "Turn-context lease TTL-evicted for execution run {ExecutionRunId} after {LeaseAgeHours:F1}h without a terminal-cleanup Remove call. This is a backstop eviction, not the primary cleanup path — an actively waiting run's lease is never evicted this way.",
            eviction.ExecutionRunId,
            eviction.Age.TotalHours));

    public event EventHandler<ExecutionLogEntry>? ExecutionUpdated
    {
        add
        {
            if (value is not null)
            {
                executionUpdatedDispatcher.Subscribe(value);
            }
        }
        remove
        {
            if (value is not null)
            {
                executionUpdatedDispatcher.Unsubscribe(value);
            }
        }
    }

    private void NotifyExecutionUpdated(ExecutionLogEntry entry)
    {
        executionUpdatedDispatcher.Publish(this, entry);
    }

    public void Dispose()
    {
        executionUpdatedDispatcher.Dispose();
    }

    private static IsolatedCompatibilityEventDispatcher<ExecutionLogEntry>
        CreateExecutionUpdatedDispatcher(ILogger logger)
    {
        return new(
            failure => logger.LogWarning(
                failure.Exception,
                "ExecutionUpdated subscriber failed for execution run {ExecutionRunId}, agent {AgentId}, chat session {ChatSessionId}, event {ExecutionEventId}, phase {Phase}, and state {ExecutionState}.",
                failure.Event.ExecutionRunId,
                failure.Event.AgentId,
                failure.Event.ChatSessionId,
                failure.Event.Id,
                failure.Event.Phase,
                failure.Event.State),
            overflow => logger.LogWarning(
                "ExecutionUpdated subscriber mailbox overflow dropped {DroppedEventCount} update(s) at capacity {MailboxCapacity} while preserving canonical execution. Latest dropped identity: execution run {ExecutionRunId}, agent {AgentId}, chat session {ChatSessionId}, event {ExecutionEventId}, phase {Phase}, and state {ExecutionState}.",
                overflow.DroppedEventCount,
                overflow.MailboxCapacity,
                overflow.LastDroppedEvent.ExecutionRunId,
                overflow.LastDroppedEvent.AgentId,
                overflow.LastDroppedEvent.ChatSessionId,
                overflow.LastDroppedEvent.Id,
                overflow.LastDroppedEvent.Phase,
                overflow.LastDroppedEvent.State));
    }
}
