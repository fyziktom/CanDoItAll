using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class WorkspaceImageAnalysisModelResolver
{
    public static string ResolveProviderImageAnalysisModel(
        ProviderProfile provider,
        string? runtimeModel) =>
        AgentImageAnalysisModelPolicy.ResolveProviderImageAnalysisModel(provider, runtimeModel);
}
