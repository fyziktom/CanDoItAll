using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessAgentRuntimeToolProvider
{
    private static ProcessRuntimeToolPurposePolicy ResolvePurposePolicy(
        AgentRuntimeToolProviderPurpose purpose)
        => purpose switch
        {
            AgentRuntimeToolProviderPurpose.InteractiveChat => ProcessRuntimeToolPurposePolicy.ReadWrite,
            AgentRuntimeToolProviderPurpose.GovernedProcessAutomation => ProcessRuntimeToolPurposePolicy.ReadWrite,
            AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive => ProcessRuntimeToolPurposePolicy.ReadWrite,
            AgentRuntimeToolProviderPurpose.A2AEndpoint => ProcessRuntimeToolPurposePolicy.ReadWrite,
            _ => ProcessRuntimeToolPurposePolicy.None
        };

    private static bool ShouldExposeTool(
        ProcessAccessState accessState,
        ProcessRuntimeToolPurposePolicy purposePolicy,
        string toolName)
    {
        if (AgentToolInvocationPolicyMetadata.IsMutationTool(toolName))
        {
            return purposePolicy.AllowMutationTools && accessState.CanWrite;
        }

        return purposePolicy.AllowReadTools && accessState.CanRead;
    }

    private readonly record struct ProcessRuntimeToolPurposePolicy(
        bool AllowReadTools,
        bool AllowMutationTools)
    {
        public static ProcessRuntimeToolPurposePolicy None { get; } = new(false, false);

        public static ProcessRuntimeToolPurposePolicy ReadWrite { get; } = new(true, true);
    }
}
