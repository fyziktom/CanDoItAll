using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.AgentFramework.UI.Shell;

public sealed record AgentsShellOrigin(long Scope, string Tab);

public enum AgentsShellCommand { RetryHeader, OpenHrAgent, OpenCrmHrAgents, OpenWorkflows, OpenProcesses, FeedDefaults }

public abstract record AgentsShellIntent(AgentsShellOrigin Origin) {
    public sealed record Command(AgentsShellOrigin Origin, AgentsShellCommand Action) : AgentsShellIntent(Origin);
    public sealed record SelectTab(AgentsShellOrigin Origin, string Key) : AgentsShellIntent(Origin);
}

public sealed record AgentsShellState(
    AgentsShellOrigin Origin,
    string TechnicalAgents,
    string Providers,
    string BoundResources,
    string Capabilities,
    string ActiveRuns,
    string FailedRuns,
    string? HeaderFailure,
    bool HeaderLoading,
    string HrAgentName,
    string? HrAgentAvatar,
    bool HrReady,
    bool OpeningHrAgent,
    IReadOnlyList<SecondaryTabItem> Tabs,
    bool ConfirmingDefaults,
    bool FeedingDefaults);
