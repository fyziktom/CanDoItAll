using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService(
    ISandboxWorkspaceStore store,
    IAgentExecutionReportReader executionReportReader,
    IAgentRuntime runtime,
    IAgentExecutionGovernanceBridge executionGovernanceBridge,
    IAgentExecutionEventSink executionEventSink,
    IAgentExecutionCheckpointBridge executionCheckpointBridge,
    IProviderRuntimeProfileSource providerSource,
    IAgentProviderCredentialResolver providerCredentialResolver,
    ILogger logger,
    AgentExecutionActivityWorkspaceIdentity activityWorkspaceIdentity,
    IAgentExecutionPreparationService executionPreparationService,
    IAgentExecutionCancellationRegistry? executionCancellationRegistry = null,
    IAgentOutputRepairService? outputRepairService = null,
    IWorkspacePathResolutionService? workspacePathResolutionService = null) :
    IDisposable
{
    private readonly IAgentOutputRepairService outputRepairService =
        outputRepairService ?? JsonObjectExtractionAgentOutputRepairService.Instance;
    private readonly IAgentExecutionCancellationRegistry executionCancellationRegistry =
        executionCancellationRegistry ?? new AgentExecutionCancellationRegistry();
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
    private readonly ILogger logger = logger;
    private readonly IsolatedCompatibilityEventDispatcher<ExecutionLogEntry> executionUpdatedDispatcher =
        CreateExecutionUpdatedDispatcher(logger);
    private readonly AgentRunTransientContextRegistry transientContextRegistry = new();
    private static readonly ProviderProfileService ProviderFeatureService = new();

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
