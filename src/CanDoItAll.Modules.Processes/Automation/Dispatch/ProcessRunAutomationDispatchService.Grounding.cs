using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private async Task<PrefetchedProjectStructureGrounding> TryBuildProjectStructureGroundingAsync(
        DispatchCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (!ProcessProjectStructureContextFormatter.TryParse(candidate.Run.TriggerReason, out var projectStructureContext) ||
            projectStructureContext is null)
        {
            return PrefetchedProjectStructureGrounding.Empty;
        }

        string? projectName = null;
        IReadOnlyList<ProjectStructureGroundingNodeData> surfaceNodes = [];
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var projectWorkbenchServiceType = Type.GetType("CanDoItAll.Modules.Workbench.ProjectWorkbenchService, CanDoItAll.Modules.Workbench");
            if (projectWorkbenchServiceType is null)
            {
                logger.LogDebug(
                    "Project workbench service type was unavailable while building project structure grounding for process run {RunId}, step {StepRunId}. Falling back to canonical workbench nodes only.",
                    candidate.Run.Id,
                    candidate.StepRun.Id);
            }
            else
            {
                var projectWorkbenchService = scope.ServiceProvider.GetService(projectWorkbenchServiceType);
                if (projectWorkbenchService is null)
                {
                    logger.LogDebug(
                        "Project workbench service was unavailable while building project structure grounding for process run {RunId}, step {StepRunId}. Falling back to canonical workbench nodes only.",
                        candidate.Run.Id,
                        candidate.StepRun.Id);
                }
                else
                {
                    var getStructureAsync = projectWorkbenchServiceType.GetMethod(
                        "GetStructureAsync",
                        [typeof(Guid), typeof(CancellationToken)]);
                    if (getStructureAsync is null)
                    {
                        logger.LogDebug(
                            "Project workbench service did not expose GetStructureAsync(Guid, CancellationToken) while building project structure grounding for process run {RunId}, step {StepRunId}. Falling back to canonical workbench nodes only.",
                            candidate.Run.Id,
                            candidate.StepRun.Id);
                    }
                    else
                    {
                        var surfaceTask = getStructureAsync.Invoke(projectWorkbenchService, [projectStructureContext.ProjectId, cancellationToken]) as Task;
                        if (surfaceTask is not null)
                        {
                            await surfaceTask;
                            var surface = surfaceTask.GetType().GetProperty("Result")?.GetValue(surfaceTask);
                            if (surface is not null)
                            {
                                projectName = GetProjectStructureGroundingString(surface, "ProjectName");
                                surfaceNodes = ExtractProjectStructureGroundingNodes(surface);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not prefetch projected project structure grounding for process run {RunId}, step {StepRunId}, project {ProjectId}. Falling back to canonical workbench nodes only.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                projectStructureContext.ProjectId);
        }

        var canonicalNodes = await TryLoadCanonicalProjectStructureGroundingNodesAsync(projectStructureContext.ProjectId, cancellationToken);
        if (surfaceNodes.Count == 0 && canonicalNodes.Count == 0)
        {
            return PrefetchedProjectStructureGrounding.Empty;
        }

        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = await TryResolveProjectStructureProjectNameAsync(projectStructureContext.ProjectId, cancellationToken);
        }

        var promptSummary = BuildProjectStructureGroundingSummary(
            string.IsNullOrWhiteSpace(projectName)
                ? projectStructureContext.ProjectId.ToString("D")
                : projectName,
            surfaceNodes,
            canonicalNodes,
            projectStructureContext);
        return string.IsNullOrWhiteSpace(promptSummary)
            ? PrefetchedProjectStructureGrounding.Empty
            : new PrefetchedProjectStructureGrounding(
                promptSummary,
                ["project_structure_read"]);
    }

    private async Task<PrefetchedArtifactInspectionGrounding> TryBuildArtifactInspectionGroundingAsync(
        DispatchCandidate candidate,
        CancellationToken cancellationToken)
    {
        var requiresUpstreamValidationReceiptGrounding = RequiresConcreteImplementationReview(candidate) ||
                                                        RequiresConcreteBrowserProof(candidate);
        if (candidate.ArtifactInputs.Count == 0 && !requiresUpstreamValidationReceiptGrounding)
        {
            return PrefetchedArtifactInspectionGrounding.Empty;
        }

        var artifactEntries = candidate.ArtifactInputs
            .SelectMany(
                artifactInput => artifactInput.Artifacts.Select(artifact => new
                {
                    artifactInput.SourceStepTitle,
                    artifactInput.ExpectedArtifactTitle,
                    Artifact = artifact
                }))
            .Where(item => !string.IsNullOrWhiteSpace(item.Artifact.ManagedStoragePath))
            .GroupBy(
                item => WorkspaceScopeDescriptor.NormalizeRelativePath(item.Artifact.ManagedStoragePath),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(4)
            .ToList();
        if (artifactEntries.Count == 0 && !requiresUpstreamValidationReceiptGrounding)
        {
            return PrefetchedArtifactInspectionGrounding.Empty;
        }

        try
        {
            var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
            var builder = new StringBuilder();
            var satisfiedToolNames = new HashSet<string>(StringComparer.Ordinal);
            var appendedArtifactCount = 0;

            if (artifactEntries.Count > 0)
            {
                builder.AppendLine("Dispatcher pre-inspected recorded upstream durable artifacts before this step started:");
                foreach (var artifactEntry in artifactEntries)
                {
                    var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(artifactEntry.Artifact.ManagedStoragePath);
                    if (string.IsNullOrWhiteSpace(normalizedPath))
                    {
                        continue;
                    }

                    if (!TryResolveArtifactFullPath(workspaceRoot, normalizedPath, out var fullPath, out _) ||
                        !File.Exists(fullPath))
                    {
                        continue;
                    }

                    var fileInfo = new FileInfo(fullPath);
                    satisfiedToolNames.Add("workspace_stat_path");
                    builder.Append("- `");
                    builder.Append(normalizedPath);
                    builder.Append("` from ");
                    builder.Append(artifactEntry.SourceStepTitle);
                    builder.Append(" -> ");
                    builder.Append(artifactEntry.Artifact.Title);
                    builder.Append(" (");
                    builder.Append(fileInfo.Length);
                    builder.Append(" bytes");
                    if (fileInfo.LastWriteTimeUtc != default)
                    {
                        builder.Append(", updated ");
                        builder.Append(fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
                    }

                    builder.AppendLine(")");

                    if (!string.IsNullOrWhiteSpace(artifactEntry.Artifact.ReviewSummary))
                    {
                        builder.Append("  Review summary: ");
                        builder.AppendLine(TrimForPrompt(artifactEntry.Artifact.ReviewSummary, 280));
                    }

                    if (!string.IsNullOrWhiteSpace(artifactEntry.Artifact.ProvenanceSummary))
                    {
                        builder.Append("  Provenance: ");
                        builder.AppendLine(TrimForPrompt(artifactEntry.Artifact.ProvenanceSummary, 280));
                    }

                    if (IsTextReadableManagedArtifactPath(normalizedPath))
                    {
                        var fileContents = await File.ReadAllTextAsync(fullPath, cancellationToken);
                        satisfiedToolNames.Add("workspace_read_file");
                        builder.Append("  Excerpt: ");
                        builder.AppendLine(string.IsNullOrWhiteSpace(fileContents)
                            ? "(file is empty)"
                            : TrimForPrompt(CollapsePromptWhitespace(fileContents), 420));
                    }

                    appendedArtifactCount++;
                }
            }

            var appendedValidationReceiptCount = await AppendUpstreamValidationReceiptGroundingAsync(
                candidate,
                builder,
                satisfiedToolNames,
                cancellationToken);
            if (appendedArtifactCount == 0 && appendedValidationReceiptCount == 0)
            {
                return PrefetchedArtifactInspectionGrounding.Empty;
            }

            if (satisfiedToolNames.Count == 0)
            {
                return PrefetchedArtifactInspectionGrounding.Empty;
            }

            return new PrefetchedArtifactInspectionGrounding(
                builder.ToString().Trim(),
                satisfiedToolNames.ToList());
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not prefetch governed artifact inspection grounding for process run {RunId}, step {StepRunId}.",
                candidate.Run.Id,
                candidate.StepRun.Id);
            return PrefetchedArtifactInspectionGrounding.Empty;
        }
    }

    private async Task<int> AppendUpstreamValidationReceiptGroundingAsync(
        DispatchCandidate candidate,
        StringBuilder builder,
        ISet<string> satisfiedToolNames,
        CancellationToken cancellationToken)
    {
        if (!RequiresConcreteImplementationReview(candidate) &&
            !RequiresConcreteBrowserProof(candidate))
        {
            return 0;
        }

        var executionRuns = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                SourceKind: "process-step",
                ProcessRunId: candidate.Run.Id.ToString("D"),
                State: ExecutionState.Completed,
                Outcome: RunOutcome.Succeeded,
                Take: 24),
            cancellationToken);
        if (executionRuns.Count == 0)
        {
            return 0;
        }

        var appendedCount = 0;
        var wroteHeader = false;
        var seenReceiptKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var run in executionRuns
                     .Where(item => !string.Equals(item.ProcessStepId, candidate.StepRun.Id.ToString("D"), StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(item => item.CompletedAtUtc ?? item.UpdatedAtUtc))
        {
            var detail = await workspaceService.GetExecutionRunDetailAsync(run.Id, cancellationToken);
            foreach (var receipt in detail.ToolReceipts
                         .Where(IsSuccessfulUpstreamValidationReceipt)
                         .OrderByDescending(item => item.CompletedAtUtc)
                         .ThenByDescending(item => item.StartedAtUtc))
            {
                var receiptKey = string.Join(
                    "|",
                    NormalizeToolToken(receipt.ToolName),
                    receipt.RequestSummary.Trim(),
                    receipt.WorkingDirectory.Trim());
                if (!seenReceiptKeys.Add(receiptKey))
                {
                    continue;
                }

                if (!wroteHeader)
                {
                    if (builder.Length > 0)
                    {
                        builder.AppendLine();
                    }

                    builder.AppendLine("Dispatcher pre-inspected successful upstream build/test receipts before this step started:");
                    wroteHeader = true;
                }

                var normalizedToolName = NormalizeToolToken(receipt.ToolName);
                builder.Append("- `");
                builder.Append(normalizedToolName);
                builder.Append("` succeeded");

                if (!string.IsNullOrWhiteSpace(receipt.RequestSummary))
                {
                    builder.Append(" for `");
                    builder.Append(TrimForPrompt(receipt.RequestSummary.Trim(), 180));
                    builder.Append('`');
                }

                if (!string.IsNullOrWhiteSpace(receipt.WorkingDirectory))
                {
                    builder.Append(" in `");
                    builder.Append(TrimForPrompt(receipt.WorkingDirectory.Trim(), 180));
                    builder.Append('`');
                }

                builder.Append(" during upstream execution run `");
                builder.Append(run.Id.ToString("D"));
                builder.Append('`');

                var completedAtUtc = receipt.CompletedAtUtc == default
                    ? run.CompletedAtUtc ?? run.UpdatedAtUtc
                    : receipt.CompletedAtUtc;
                if (completedAtUtc != default)
                {
                    builder.Append(" at ");
                    builder.Append(completedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
                }

                if (!string.IsNullOrWhiteSpace(receipt.ExitSummary))
                {
                    builder.Append(" (");
                    builder.Append(TrimForPrompt(receipt.ExitSummary.Trim(), 120));
                    builder.Append(')');
                }

                builder.AppendLine(".");

                satisfiedToolNames.Add(normalizedToolName);
                appendedCount++;
                if (appendedCount >= 4)
                {
                    return appendedCount;
                }
            }
        }

        return appendedCount;
    }

    private async Task<IReadOnlyList<ProjectStructureGroundingNodeData>> TryLoadCanonicalProjectStructureGroundingNodesAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var connection = dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
SELECT
    NodeKey,
    COALESCE(ParentNodeKey, ''),
    ObjectType,
    COALESCE(ObjectSubtype, ''),
    COALESCE(Title, ''),
    COALESCE(Subtitle, ''),
    COALESCE(Status, ''),
    COALESCE(Notes, ''),
    COALESCE(MetadataJson, '{}')
FROM Workbench_ProjectObjects
WHERE lower(ProjectId) = lower($projectId)
  AND IsSystemManaged = 0
ORDER BY CreatedAtUtc, Title;
""";

                var projectIdParameter = command.CreateParameter();
                projectIdParameter.ParameterName = "$projectId";
                projectIdParameter.Value = projectId.ToString("D");
                command.Parameters.Add(projectIdParameter);

                var nodes = new List<ProjectStructureGroundingNodeData>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var nodeId = ReadProjectStructureGroundingColumn(reader, 0);
                    if (string.IsNullOrWhiteSpace(nodeId))
                    {
                        continue;
                    }

                    nodes.Add(new ProjectStructureGroundingNodeData(
                        nodeId,
                        ReadProjectStructureGroundingColumn(reader, 1),
                        ResolveProjectStructureObjectTypeLabel(reader.GetValue(2)),
                        ReadProjectStructureGroundingColumn(reader, 3),
                        ReadProjectStructureGroundingColumn(reader, 4),
                        ReadProjectStructureGroundingColumn(reader, 5),
                        ReadProjectStructureGroundingColumn(reader, 6),
                        ReadProjectStructureGroundingColumn(reader, 7),
                        ReadProjectStructureGroundingColumn(reader, 8)));
                }

                return nodes;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not load canonical workbench nodes for project structure grounding on project {ProjectId}.",
                projectId);
            return [];
        }
    }

    private async Task<string> TryResolveProjectStructureProjectNameAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Set<Project>()
                .Where(item => item.Id == projectId)
                .Select(item => item.Name)
                .SingleOrDefaultAsync(cancellationToken)
                ?? string.Empty;
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "Could not resolve project name while building project structure grounding for project {ProjectId}.",
                projectId);
            return string.Empty;
        }
    }

    private static string BuildProjectStructureGroundingSummary(
        object surface,
        ProcessProjectStructureContext context)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(context);

        var projectName = GetProjectStructureGroundingString(surface, "ProjectName");
        var nodes = ExtractProjectStructureGroundingNodes(surface);
        return BuildProjectStructureGroundingSummary(projectName, nodes, [], context);
    }

    private static string BuildProjectStructureGroundingSummary(
        string projectName,
        IReadOnlyList<ProjectStructureGroundingNodeData> surfaceNodes,
        IReadOnlyList<ProjectStructureGroundingNodeData> supplementalNodes,
        ProcessProjectStructureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var nodes = MergeProjectStructureGroundingNodes(surfaceNodes, supplementalNodes);
        return BuildProjectStructureGroundingSummary(projectName, nodes, context);
    }

    private static string BuildProjectStructureGroundingSummary(
        string projectName,
        IReadOnlyList<ProjectStructureGroundingNodeData> nodes,
        ProcessProjectStructureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (nodes.Count == 0)
        {
            return string.Empty;
        }

        var nodesById = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var nodesByParentId = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
            .GroupBy(node => NormalizeProjectStructureNodeId(node.ParentId), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProjectStructureGroundingNodeData>)group.ToList(),
                StringComparer.Ordinal);
        var targetNodeId = NormalizeProjectStructureNodeId(context.ResolveTargetNodeId());
        var selectedProcessNodeId = NormalizeProjectStructureNodeId(context.NodeId);
        var builder = new StringBuilder();
        builder.AppendLine($"Dispatcher fetched the live project structure for `{projectName}` and focused this prompt on the selected work branch.");

        var ancestorPath = ResolveProjectStructureAncestorPath(targetNodeId, nodesById);
        if (ancestorPath.Count > 0)
        {
            builder.AppendLine("Ancestor path to the target work node:");
            AppendProjectStructureGroundingNodes(builder, ancestorPath);
        }

        if (!string.IsNullOrWhiteSpace(selectedProcessNodeId) &&
            nodesById.TryGetValue(selectedProcessNodeId, out var selectedProcessNode) &&
            !string.Equals(selectedProcessNodeId, targetNodeId, StringComparison.Ordinal))
        {
            builder.AppendLine("Selected process node:");
            AppendProjectStructureGroundingNodes(builder, [selectedProcessNode]);
        }

        if (!string.IsNullOrWhiteSpace(targetNodeId) &&
            nodesById.TryGetValue(targetNodeId, out var targetNode))
        {
            var siblingNodes = nodes
                .Where(node =>
                    !string.Equals(node.Id, targetNode.Id, StringComparison.Ordinal) &&
                    !string.Equals(node.Id, selectedProcessNodeId, StringComparison.Ordinal) &&
                    string.Equals(node.ParentId, targetNode.ParentId, StringComparison.Ordinal))
                .Select(node => new
                {
                    Node = node,
                    SignalScore = GetProjectStructureGroundingSignalScore(node)
                })
                .Where(item => item.SignalScore > 0 || !string.IsNullOrWhiteSpace(item.Node.Title))
                .OrderByDescending(item => item.SignalScore)
                .ThenBy(item => item.Node.Title, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .Select(item => item.Node)
                .ToList();

            if (siblingNodes.Count > 0)
            {
                builder.AppendLine("Sibling planning context under the same parent:");
                AppendProjectStructureGroundingNodes(builder, siblingNodes);
            }

            var siblingDescendantNodes = siblingNodes
                .SelectMany(node => ResolveProjectStructureDescendants(node.Id, nodesByParentId, maxDepth: 3))
                .Where(node =>
                    !string.Equals(node.Id, targetNode.Id, StringComparison.Ordinal) &&
                    !string.Equals(node.Id, selectedProcessNodeId, StringComparison.Ordinal) &&
                    !IsProjectStructureGroundingNoiseNode(node))
                .Select(node => new
                {
                    Node = node,
                    SignalScore = GetProjectStructureGroundingSignalScore(node)
                })
                .Where(item => item.SignalScore > 0 || !string.IsNullOrWhiteSpace(item.Node.Title))
                .OrderByDescending(item => item.SignalScore)
                .ThenBy(item => item.Node.Title, StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .Select(item => item.Node)
                .ToList();

            if (siblingDescendantNodes.Count > 0)
            {
                builder.AppendLine("Descendant requirement context from sibling planning nodes:");
                AppendProjectStructureGroundingNodes(builder, siblingDescendantNodes);
            }

            var childNodes = nodes
                .Where(node =>
                    string.Equals(node.ParentId, targetNode.Id, StringComparison.Ordinal) &&
                    !string.Equals(node.Id, selectedProcessNodeId, StringComparison.Ordinal) &&
                    !IsProjectStructureGroundingNoiseNode(node))
                .OrderBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();

            if (childNodes.Count > 0)
            {
                builder.AppendLine("Immediate child nodes under the target work node:");
                AppendProjectStructureGroundingNodes(builder, childNodes);
            }
        }

        return builder.ToString().Trim();
    }

    private static IReadOnlyList<ProjectStructureGroundingNodeData> MergeProjectStructureGroundingNodes(
        IReadOnlyList<ProjectStructureGroundingNodeData> primaryNodes,
        IReadOnlyList<ProjectStructureGroundingNodeData> supplementalNodes)
    {
        if (primaryNodes.Count == 0)
        {
            return supplementalNodes;
        }

        if (supplementalNodes.Count == 0)
        {
            return primaryNodes;
        }

        var merged = new Dictionary<string, ProjectStructureGroundingNodeData>(StringComparer.Ordinal);
        foreach (var node in primaryNodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                continue;
            }

            merged[node.Id] = node;
        }

        foreach (var node in supplementalNodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id) || merged.ContainsKey(node.Id))
            {
                continue;
            }

            merged[node.Id] = node;
        }

        return merged.Values.ToList();
    }

    private static string ReadProjectStructureGroundingColumn(DbDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? string.Empty
            : reader.GetValue(ordinal)?.ToString()?.Trim() ?? string.Empty;
    }

    private static string ResolveProjectStructureObjectTypeLabel(object? value)
    {
        if (value is null || value == DBNull.Value)
        {
            return string.Empty;
        }

        if (value is long longValue && Enum.IsDefined(typeof(ProjectObjectType), (int)longValue))
        {
            return ((ProjectObjectType)(int)longValue).ToString();
        }

        if (value is int intValue && Enum.IsDefined(typeof(ProjectObjectType), intValue))
        {
            return ((ProjectObjectType)intValue).ToString();
        }

        var text = value.ToString()?.Trim() ?? string.Empty;
        if (int.TryParse(text, out var parsedIntValue) &&
            Enum.IsDefined(typeof(ProjectObjectType), parsedIntValue))
        {
            return ((ProjectObjectType)parsedIntValue).ToString();
        }

        return text;
    }

}
