using System.Globalization;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessDefinitionCanvasEditorProjectionService
{
    private ProcessTemplateDefinitionSummary FindTemplateDefinition(ProcessDefinitionCatalogItemKey definitionKey)
    {
        var pack = templatePackLoader.Load();
        return pack.Definitions.FirstOrDefault(definition =>
            string.Equals(definition.Key, definitionKey.Value, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Process definition '{definitionKey.Value}' is not available in the template pack.");
    }

    private ProcessDefinitionCanvasVersionToken CreateVersionToken(ProcessDefinitionCanvasCommandKind commandKind)
        => new($"{commandKind.ToString().ToLowerInvariant()}:{clock.GetUtcNow():yyyyMMddHHmmss}:{Guid.NewGuid():N}");

    private static ProcessDefinitionCanvasNodeKey BuildUniqueNodeKey(
        string prefix,
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> nodes)
    {
        var used = nodes
            .Select(node => node.NodeKey.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = prefix;
        for (var index = 1; used.Contains(candidate); index++)
        {
            candidate = $"{prefix}-{index.ToString(CultureInfo.InvariantCulture)}";
        }

        return new ProcessDefinitionCanvasNodeKey(candidate);
    }

    private static ProcessDefinitionCanvasEdgeKey BuildUniqueEdgeKey(
        string prefix,
        IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> edges)
    {
        var used = edges
            .Select(edge => edge.EdgeKey.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = prefix;
        for (var index = 1; used.Contains(candidate); index++)
        {
            candidate = $"{prefix}-{index.ToString(CultureInfo.InvariantCulture)}";
        }

        return new ProcessDefinitionCanvasEdgeKey(candidate);
    }

    private static string Slugify(string value)
    {
        var characters = value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        var slug = new string(characters).Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "canvas-node" : slug;
    }

    private static void ValidateScope(ProcessWorkspaceShellScope scope, ProcessProjectionProjectBinding? projectBinding)
    {
        if (scope.Kind == ProcessWorkspaceScopeKind.Project && scope.ProjectId is null)
        {
            throw new ArgumentException("Project-scoped canvas command requires a project id.", nameof(scope));
        }

        if (scope.Kind == ProcessWorkspaceScopeKind.Global && scope.ProjectId is not null)
        {
            throw new ArgumentException("Global canvas command cannot carry a project id.", nameof(scope));
        }
        if (projectBinding is not null && (scope.Kind != ProcessWorkspaceScopeKind.Project || scope.ProjectId != projectBinding.ProjectId)) {
            throw new ArgumentException("The canvas project lifetime must match its project scope.", nameof(projectBinding));
        }
    }

    private readonly record struct ProcessDefinitionCanvasStateKey(
        ProcessWorkspaceScopeKind ScopeKind,
        Guid? ProjectId,
        ProcessDefinitionCatalogItemKey DefinitionKey,
        ProcessProjectionProjectBinding? ProjectBinding)
    {
        public static ProcessDefinitionCanvasStateKey From(
            ProcessWorkspaceShellScope scope,
            ProcessDefinitionCatalogItemKey definitionKey,
            ProcessProjectionProjectBinding? projectBinding)
            => new(scope.Kind, scope.ProjectId, definitionKey, projectBinding);
    }

    private sealed record ProcessDefinitionCanvasSnapshot(
        ProcessWorkspaceShellScope Scope,
        ProcessDefinitionCatalogItemKey DefinitionKey,
        ProcessDefinitionCanvasVersionToken VersionToken,
        IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> Nodes,
        IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> Edges,
        IReadOnlyList<ProcessDefinitionCanvasToolboxActionProjection> ToolboxActions,
        ProcessDefinitionCanvasSelectionProjection Selection);
}
