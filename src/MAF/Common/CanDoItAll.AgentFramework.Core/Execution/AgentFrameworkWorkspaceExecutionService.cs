using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService(
    ISandboxWorkspaceStore store,
    IAgentRuntime runtime,
    IAgentExecutionGovernanceBridge executionGovernanceBridge,
    IAgentExecutionEventSink executionEventSink,
    IAgentExecutionCheckpointBridge executionCheckpointBridge,
    IProviderProfileRegistry providerRegistry,
    IAgentProviderCredentialResolver providerCredentialResolver,
    IAgentExecutionCancellationRegistry? executionCancellationRegistry = null,
    IAgentOutputRepairService? outputRepairService = null,
    IWorkspacePathResolutionService? workspacePathResolutionService = null)
{
    private readonly IAgentOutputRepairService outputRepairService =
        outputRepairService ?? JsonObjectExtractionAgentOutputRepairService.Instance;
    private readonly IAgentExecutionCancellationRegistry executionCancellationRegistry =
        executionCancellationRegistry ?? new AgentExecutionCancellationRegistry();
    private readonly IWorkspacePathResolutionService? workspacePathResolutionService = workspacePathResolutionService;
    private readonly AgentRunTransientContextRegistry transientContextRegistry = new();
    private static readonly ProviderProfileService ProviderFeatureService = new();

    public event EventHandler<ExecutionLogEntry>? ExecutionUpdated;
}
