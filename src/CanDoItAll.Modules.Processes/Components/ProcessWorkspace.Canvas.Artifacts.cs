using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private void CreateDefinitionArtifactClone(CanvasWorkbenchContextActionRequest request)
    {
        var sourceNodeId = string.IsNullOrWhiteSpace(request.NodeId)
            ? selectedCanvasNodeId
            : request.NodeId;
        if (!TryResolveDefinitionArtifactWithOwner(sourceNodeId, out var artifact, out _))
        {
            SetError("Select an artifact node before creating an artifact clone.");
            return;
        }

        if (artifact.Id is not Guid artifactId)
        {
            SetError("Save the artifact expectation before creating canvas clones for it.");
            return;
        }

        var draftId = Guid.NewGuid();
        var draftNodeId = ProcessCanvasBranching.BuildDefinitionArtifactDraftCloneNodeId(artifactId, draftId);
        var (x, y) = ResolveDefinitionArtifactCloneDraftPosition(request, sourceNodeId);
        artifactCloneDrafts[draftNodeId] = new ProcessCanvasArtifactCloneDraft(draftNodeId, artifactId, x, y);

        var uiState = CloneCanvasUiState(ResolveStoredCanvasUiState());
        uiState.ManualPositions[draftNodeId] = new CanvasWorkbenchPoint
        {
            X = x,
            Y = y
        };
        uiState.SelectedNodeIds = [draftNodeId];
        StoreCanvasUiState(uiState);

        selectedCanvasNodeId = draftNodeId;
        RefreshCanvasSurface();
        SetMessage($"Artifact clone created for '{ResolveArtifactLabel(artifact)}'. Connect it to a step artifact input to persist the input relationship.");
    }

    private void HighlightDefinitionArtifactClones(string? nodeId)
    {
        var sourceNodeId = string.IsNullOrWhiteSpace(nodeId)
            ? selectedCanvasNodeId
            : nodeId;
        if (!TryResolveDefinitionArtifactWithOwner(sourceNodeId, out var artifact, out _))
        {
            SetError("Select an artifact node before highlighting artifact clones.");
            return;
        }

        if (artifact.Id is not Guid artifactId)
        {
            SetError("Save the artifact expectation before highlighting canvas clones for it.");
            return;
        }

        var nodeIds = ResolveDefinitionArtifactVisualNodeIds(artifactId);
        if (nodeIds.Count == 0)
        {
            SetError($"No artifact canvas nodes were found for '{ResolveArtifactLabel(artifact)}'.");
            return;
        }

        var uiState = CloneCanvasUiState(ResolveStoredCanvasUiState());
        uiState.SelectedNodeIds = [.. nodeIds];
        StoreCanvasUiState(uiState);

        selectedCanvasNodeId = null;
        RefreshCanvasSurface();
        SetMessage($"Highlighted {nodeIds.Count} canvas node(s) for '{ResolveArtifactLabel(artifact)}'.");
    }

    private bool TryConvertDraftArtifactCloneToInputClone(
        string? sourceNodeId,
        ProcessArtifactExpectationEditorModel artifact,
        ProcessStepArtifactInputEditorModel artifactInput,
        ProcessStepEditorModel targetStep)
    {
        if (string.IsNullOrWhiteSpace(sourceNodeId) ||
            !artifactCloneDrafts.Remove(sourceNodeId, out var draft))
        {
            return false;
        }

        var cloneNodeId = ProcessCanvasBranching.BuildDefinitionArtifactCloneNodeId(artifact, artifactInput, targetStep);
        var uiState = CloneCanvasUiState(ResolveStoredCanvasUiState());
        uiState.ManualPositions.Remove(sourceNodeId);
        uiState.ManualPositions[cloneNodeId] = new CanvasWorkbenchPoint
        {
            X = draft.X,
            Y = draft.Y
        };
        uiState.SelectedNodeIds = [cloneNodeId];
        StoreCanvasUiState(uiState);
        selectedCanvasNodeId = cloneNodeId;
        return true;
    }

    private bool TryResolveDefinitionArtifactWithOwner(
        string? nodeId,
        out ProcessArtifactExpectationEditorModel artifact,
        out ProcessStepEditorModel ownerStep)
    {
        artifact = default!;
        ownerStep = default!;
        if (!ProcessCanvasBranching.TryResolveDefinitionArtifactToken(nodeId, out var artifactToken))
        {
            return false;
        }

        foreach (var step in editor.Steps)
        {
            var match = Guid.TryParse(artifactToken, out var artifactId)
                ? step.ArtifactExpectations.FirstOrDefault(candidate => candidate.Id == artifactId)
                : step.ArtifactExpectations.FirstOrDefault(candidate =>
                    string.Equals(candidate.Title.Replace(' ', '-'), artifactToken, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                continue;
            }

            artifact = match;
            ownerStep = step;
            return true;
        }

        return false;
    }

    private ProcessArtifactExpectationEditorModel? ResolveDefinitionArtifact(string? nodeId)
        => TryResolveDefinitionArtifactWithOwner(nodeId, out var artifact, out _)
            ? artifact
            : null;

    private IReadOnlyList<string> ResolveDefinitionArtifactVisualNodeIds(Guid artifactId)
    {
        var artifactToken = artifactId.ToString("D");
        var nodeIds = canvasSurface?.Nodes
            .Where(node =>
                ProcessCanvasBranching.TryResolveDefinitionArtifactToken(node.Id, out var token) &&
                string.Equals(token, artifactToken, StringComparison.Ordinal))
            .Select(node => node.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];
        if (nodeIds.Count > 0)
        {
            return nodeIds;
        }

        if (!TryResolveDefinitionArtifactById(artifactId, out var artifact, out _))
        {
            return [];
        }

        var fallbackNodeIds = new List<string>
        {
            ProcessCanvasBranching.BuildDefinitionArtifactNodeId(artifact)
        };
        fallbackNodeIds.AddRange(editor.Steps
            .SelectMany(step => step.ArtifactInputs
                .Where(input => input.ArtifactExpectationId == artifactId)
                .Select(input => ProcessCanvasBranching.BuildDefinitionArtifactCloneNodeId(artifact, input, step))));
        fallbackNodeIds.AddRange(artifactCloneDrafts.Values
            .Where(draft => draft.ArtifactExpectationId == artifactId)
            .Select(draft => draft.NodeId));
        return fallbackNodeIds
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private bool TryResolveDefinitionArtifactById(
        Guid artifactId,
        out ProcessArtifactExpectationEditorModel artifact,
        out ProcessStepEditorModel ownerStep)
    {
        artifact = default!;
        ownerStep = default!;
        foreach (var step in editor.Steps)
        {
            var match = step.ArtifactExpectations.FirstOrDefault(candidate => candidate.Id == artifactId);
            if (match is null)
            {
                continue;
            }

            artifact = match;
            ownerStep = step;
            return true;
        }

        return false;
    }

    private (double X, double Y) ResolveDefinitionArtifactCloneDraftPosition(
        CanvasWorkbenchContextActionRequest request,
        string? sourceNodeId)
    {
        if (request.X != 0 || request.Y != 0)
        {
            return (request.X + 40d, request.Y + 40d);
        }

        var uiState = ResolveStoredCanvasUiState();
        if (!string.IsNullOrWhiteSpace(sourceNodeId) &&
            uiState.ManualPositions.TryGetValue(sourceNodeId, out var manualPosition))
        {
            return (manualPosition.X + 280d, manualPosition.Y + 220d);
        }

        var sourceNode = canvasSurface?.Nodes.FirstOrDefault(node =>
            string.Equals(node.Id, sourceNodeId, StringComparison.Ordinal));
        if (sourceNode is not null)
        {
            return (sourceNode.X + 280d, sourceNode.Y + 220d);
        }

        return (360d, 360d);
    }

    private static string ResolveArtifactLabel(ProcessArtifactExpectationEditorModel artifact)
        => string.IsNullOrWhiteSpace(artifact.Title)
            ? "artifact"
            : artifact.Title;
}
