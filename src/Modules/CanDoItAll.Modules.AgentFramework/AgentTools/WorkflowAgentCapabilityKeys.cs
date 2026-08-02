using System.Collections.Frozen;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public static class WorkflowAgentCapabilityKeys
{
    public const string DefinitionsList = WorkflowRuntimeCapabilityKeys.DefinitionsList;
    public const string RunStart = WorkflowRuntimeCapabilityKeys.RunStart;
    public const string RunStatusGet = WorkflowRuntimeCapabilityKeys.RunStatusGet;
    public const string RunCancel = WorkflowRuntimeCapabilityKeys.RunCancel;
    public const string ExternalResponseSubmit = WorkflowRuntimeCapabilityKeys.ExternalResponseSubmit;

    public static IReadOnlyDictionary<string, string> ToolNameToCapabilityKey { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList] = DefinitionsList,
            [AgentToolInvocationPolicyMetadata.WorkflowsRunStart] = RunStart,
            [AgentToolInvocationPolicyMetadata.WorkflowsRunStatusGet] = RunStatusGet,
            [AgentToolInvocationPolicyMetadata.WorkflowsRunCancel] = RunCancel,
            [AgentToolInvocationPolicyMetadata.WorkflowsExternalResponseSubmit] = ExternalResponseSubmit
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlySet<string> Keys => WorkflowRuntimeCapabilityKeys.Keys;
}
