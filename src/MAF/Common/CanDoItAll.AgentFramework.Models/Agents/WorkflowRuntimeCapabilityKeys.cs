using System.Collections.Frozen;

namespace CanDoItAll.AgentFramework.Models;

public static class WorkflowRuntimeCapabilityKeys
{
    public const string DefinitionsList = "workflows-definitions-list";
    public const string RunStart = "workflows-run-start";
    public const string RunStatusGet = "workflows-run-status-get";
    public const string RunCancel = "workflows-run-cancel";
    public const string ExternalResponseSubmit = "workflows-external-response-submit";

    public static IReadOnlySet<string> Keys { get; } = new[]
    {
        DefinitionsList,
        RunStart,
        RunStatusGet,
        RunCancel,
        ExternalResponseSubmit
    }.ToFrozenSet(StringComparer.Ordinal);
}
