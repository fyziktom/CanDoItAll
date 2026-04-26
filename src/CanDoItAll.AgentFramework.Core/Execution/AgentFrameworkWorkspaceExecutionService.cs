using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService(
    ISandboxWorkspaceStore store,
    IAgentRuntime runtime,
    IAgentExecutionGovernanceBridge executionGovernanceBridge,
    IAgentExecutionEventSink executionEventSink,
    IAgentExecutionCheckpointBridge executionCheckpointBridge,
    IProviderProfileRegistry providerRegistry,
    IAgentProviderCredentialResolver providerCredentialResolver)
{
    public event EventHandler<ExecutionLogEntry>? ExecutionUpdated;
}
