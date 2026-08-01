using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Resources;

public enum ResourcesAgentChatView
{
    Registry,
    Browse
}

public readonly record struct ResourceBrowseAgentChatSourceId
{
    public ResourceBrowseAgentChatSourceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }
}

public sealed record ResourceBrowseAgentChatPosition(
    ResourceBrowseAgentChatSourceId SourceId,
    string SourceClass,
    string DisplayName,
    Guid? ProjectId,
    string? ProjectName);

public sealed record ResourceBrowseAgentChatContextState(
    AgentChatContextAccessState AccessState,
    ResourceBrowseAgentChatPosition? Position)
{
    public static ResourceBrowseAgentChatContextState Loading { get; } = new(
        AgentChatContextAccessState.Loading,
        null);

    public static ResourceBrowseAgentChatContextState Failed { get; } = new(
        AgentChatContextAccessState.Failed,
        null);

    public static ResourceBrowseAgentChatContextState Ready(ResourceBrowseAgentChatPosition? position = null)
        => new(AgentChatContextAccessState.Ready, position);
}

public static class ResourcesAgentChatContextBuilder
{
    private static readonly AgentChatContextSource Source = new(
        new AgentChatContextSourceKind("resources"),
        new AgentChatContextSourceId("resources"));

    public static AgentChatContextSurface Build(
        ResourcesAgentChatView view,
        ResourceEditorModel editor,
        string? selectedProjectName,
        string connectorDisplayName,
        ResourceBrowseAgentChatPosition? browsePosition)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (!Enum.IsDefined(view))
        {
            throw new ArgumentOutOfRangeException(nameof(view), view, "The Resources agent-chat view is undefined.");
        }

        var position = view switch
        {
            ResourcesAgentChatView.Registry => BuildRegistryPosition(editor, selectedProjectName, connectorDisplayName),
            ResourcesAgentChatView.Browse => BuildBrowsePosition(browsePosition),
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "The Resources agent-chat view is undefined.")
        };

        return new AgentChatContextSurface(
            Source,
            "Resources",
            position,
            accessMode: AgentChatContextScopeAccessMode.Unrestricted);
    }

    private static AgentChatSurfacePosition BuildRegistryPosition(
        ResourceEditorModel editor,
        string? selectedProjectName,
        string connectorDisplayName)
    {
        var primarySelection = editor.Id.HasValue
            ? new AgentChatContextEntityReference(
                "resource",
                editor.Id.Value.ToString("D"),
                string.IsNullOrWhiteSpace(editor.Name) ? "Unnamed resource" : editor.Name)
            : null;
        var selectedEntities = editor.ProjectId.HasValue && !string.IsNullOrWhiteSpace(selectedProjectName)
            ?
            [
                new AgentChatContextEntityReference(
                    "project",
                    editor.ProjectId.Value.ToString("D"),
                    selectedProjectName)
            ]
            : Array.Empty<AgentChatContextEntityReference>();
        IReadOnlyList<AgentChatContextPositionFact> facts = editor.Id.HasValue
            ?
            [
                new AgentChatContextPositionFact("editor-mode", "existing"),
                new AgentChatContextPositionFact("connector", connectorDisplayName),
                new AgentChatContextPositionFact("validation", editor.ValidationStatus.ToString()),
                new AgentChatContextPositionFact("sensitivity", editor.Sensitivity.ToString())
            ]
            : [new AgentChatContextPositionFact("editor-mode", "new")];

        return new AgentChatSurfacePosition(
            module: "resources",
            surface: "registry",
            view: "registry",
            route: "/resources",
            primarySelection,
            selectedEntities,
            facts);
    }

    private static AgentChatSurfacePosition BuildBrowsePosition(ResourceBrowseAgentChatPosition? browsePosition)
    {
        var primarySelection = browsePosition is null
            ? null
            : new AgentChatContextEntityReference(
                "resource-source",
                browsePosition.SourceId.Value,
                browsePosition.DisplayName);
        var selectedEntities = browsePosition is { ProjectId: { } projectId, ProjectName: not null } &&
                               !string.IsNullOrWhiteSpace(browsePosition.ProjectName)
            ?
            [
                new AgentChatContextEntityReference(
                    "project",
                    projectId.ToString("D"),
                    browsePosition.ProjectName)
            ]
            : Array.Empty<AgentChatContextEntityReference>();

        return new AgentChatSurfacePosition(
            module: "resources",
            surface: "storage-browser",
            view: "browse",
            route: "/resources",
            primarySelection,
            selectedEntities,
            browsePosition is null
                ? []
                : [new AgentChatContextPositionFact("source-class", browsePosition.SourceClass)]);
    }
}
