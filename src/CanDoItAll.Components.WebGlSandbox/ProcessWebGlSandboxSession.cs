using CanDoItAll.Components.WebGlLib;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Components.WebGlSandbox;

public sealed class ProcessWebGlSandboxSession
{
    private const int MaxCommandLogEntries = 12;
    private const double MinNodeSpacingFactor = 0.75d;
    private const double MaxNodeSpacingFactor = 1.85d;
    private const double NodeSpacingStep = 0.15d;

    private readonly ProcessWebGlSceneAdapter sceneAdapter;
    private readonly List<ProcessWebGlCommandLogEntry> commandLog = [];
    private readonly Dictionary<string, WebGlNodePositionChange> nodePositionOverrides = new(StringComparer.Ordinal);
    private WebGlWorkbenchCameraState cameraState = new();
    private IReadOnlyList<ProcessWebGlTemplateDescriptor>? templates;
    private ProcessDefinitionEditorModel? workingEditor;

    public ProcessWebGlSandboxSession(ProcessWebGlSceneAdapter sceneAdapter)
    {
        this.sceneAdapter = sceneAdapter;
    }

    public IReadOnlyList<ProcessWebGlTemplateDescriptor> Templates
        => templates ??= sceneAdapter.ListRepresentativeTemplates();

    public string SelectedTemplateKey { get; private set; } = string.Empty;

    public string ProjectionMode { get; private set; } = WebGlWorkbenchProjectionModes.Perspective;

    public string ViewPreset { get; private set; } = WebGlWorkbenchViewPresets.Overview;

    public string LayoutMode { get; private set; } = WebGlWorkbenchLayoutModes.CenterLane;

    public double NodeSpacingFactor { get; private set; } = 1;

    public string? SelectedNodeId { get; private set; }

    public bool ShowDiagnostics { get; private set; } = true;

    public int LastExportCharacterCount { get; private set; }

    public IReadOnlyList<ProcessWebGlCommandLogEntry> CommandLog => commandLog;

    public ProcessWebGlTemplateDescriptor CurrentTemplate
    {
        get
        {
            EnsureInitialized();
            return Templates.First(template =>
                string.Equals(template.Key, SelectedTemplateKey, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void EnsureInitialized()
    {
        if (!string.IsNullOrWhiteSpace(SelectedTemplateKey) && workingEditor is not null)
        {
            return;
        }

        LoadTemplate(Templates.First().Key);
    }

    public void ApplyRouteState(string? templateKey, string? projectionMode, string? viewPreset)
    {
        EnsureInitialized();

        var resolvedTemplateKey = ResolveTemplateKey(templateKey);
        if (!string.Equals(SelectedTemplateKey, resolvedTemplateKey, StringComparison.OrdinalIgnoreCase))
        {
            LoadTemplate(resolvedTemplateKey);
        }

        SetProjectionMode(projectionMode);
        SetViewPreset(viewPreset);
    }

    public void LoadTemplate(string templateKey)
    {
        var resolvedTemplateKey = ResolveTemplateKey(templateKey);
        workingEditor = sceneAdapter.LoadProjectedDefinition(resolvedTemplateKey);
        SelectedTemplateKey = resolvedTemplateKey;
        cameraState = CreateDefaultCameraState();
        nodePositionOverrides.Clear();
        SelectedNodeId = null;
        LastExportCharacterCount = 0;
        commandLog.Clear();
        RecordCommand("Loaded template", CurrentTemplate.DisplayName);
    }

    public void Reset()
    {
        EnsureInitialized();
        workingEditor = sceneAdapter.LoadProjectedDefinition(SelectedTemplateKey);
        cameraState = CreateDefaultCameraState();
        nodePositionOverrides.Clear();
        SelectedNodeId = null;
        LastExportCharacterCount = 0;
        commandLog.Clear();
        RecordCommand("Reset sandbox", CurrentTemplate.DisplayName);
    }

    public void Recompose(string? layoutMode = null)
    {
        EnsureInitialized();
        LayoutMode = WebGlWorkbenchLayoutModes.Normalize(layoutMode ?? LayoutMode);
        nodePositionOverrides.Clear();
        cameraState = CreateDefaultCameraState();
        RecordCommand("Recomposed scene", $"{LayoutMode} · {NodeSpacingFactor:0.##}x spacing");
    }

    public void AdjustNodeSpacing(int direction)
    {
        EnsureInitialized();
        if (direction == 0)
        {
            return;
        }

        var nextFactor = ClampNodeSpacingFactor(NodeSpacingFactor + (NodeSpacingStep * Math.Sign(direction)));
        if (Math.Abs(nextFactor - NodeSpacingFactor) < 0.001d)
        {
            return;
        }

        NodeSpacingFactor = nextFactor;
        nodePositionOverrides.Clear();
        cameraState = CreateDefaultCameraState();
        RecordCommand("Adjusted spacing", $"{NodeSpacingFactor:0.##}x");
    }

    public void SetProjectionMode(string? projectionMode)
    {
        var normalized = WebGlWorkbenchProjectionModes.Perspective;
        if (string.Equals(ProjectionMode, normalized, StringComparison.Ordinal))
        {
            return;
        }

        ProjectionMode = normalized;
        cameraState.ProjectionMode = normalized;
        RecordCommand("Changed camera", ProjectionMode);
    }

    public void SetViewPreset(string? viewPreset)
    {
        var normalized = viewPreset switch
        {
            WebGlWorkbenchViewPresets.Roles => WebGlWorkbenchViewPresets.Roles,
            WebGlWorkbenchViewPresets.Dependencies => WebGlWorkbenchViewPresets.Dependencies,
            WebGlWorkbenchViewPresets.Branching => WebGlWorkbenchViewPresets.Branching,
            WebGlWorkbenchViewPresets.Focus => WebGlWorkbenchViewPresets.Focus,
            _ => WebGlWorkbenchViewPresets.Overview
        };
        if (string.Equals(ViewPreset, normalized, StringComparison.Ordinal))
        {
            return;
        }

        ViewPreset = normalized;
        RecordCommand("Changed preset", ViewPreset);
    }

    public void ToggleDiagnostics()
    {
        ShowDiagnostics = !ShowDiagnostics;
        RecordCommand("Toggled diagnostics", ShowDiagnostics ? "Visible" : "Hidden");
    }

    public void SetSelectedNode(string? nodeId)
    {
        SelectedNodeId = string.IsNullOrWhiteSpace(nodeId)
            ? null
            : nodeId;
    }

    public void ApplyNodesMoved(IReadOnlyList<WebGlNodePositionChange> positions)
    {
        EnsureInitialized();
        if (workingEditor is null || positions.Count == 0)
        {
            return;
        }

        foreach (var position in positions)
        {
            nodePositionOverrides[position.NodeId] = position;
        }

        SelectedNodeId = positions[0].NodeId;
        RecordCommand("Moved node", positions[0].NodeId);
    }

    public bool ApplyConnectionChange(WebGlConnectionChangeRequest request)
    {
        EnsureInitialized();
        if (workingEditor is null)
        {
            return false;
        }

        var changed = sceneAdapter.ApplyConnectionChange(workingEditor, request);
        if (!changed)
        {
            return false;
        }

        RecordCommand(
            string.Equals(request.ActionId, WebGlWorkbenchConnectionActions.Disconnect, StringComparison.Ordinal)
                ? "Removed connection"
                : "Created connection",
            $"{request.SourceNodeId} -> {request.TargetNodeId}");
        return true;
    }

    public void RecordExport(int imageLength)
    {
        LastExportCharacterCount = imageLength;
        RecordCommand(
            "Exported image",
            LastExportCharacterCount <= 0
                ? "No image payload"
                : $"{LastExportCharacterCount} base64 characters");
    }

    public void ApplyUiState(WebGlWorkbenchUiState uiState)
    {
        ArgumentNullException.ThrowIfNull(uiState);

        cameraState = new WebGlWorkbenchCameraState
        {
            ProjectionMode = string.Equals(uiState.Camera?.ProjectionMode, WebGlWorkbenchProjectionModes.Perspective, StringComparison.Ordinal)
                ? WebGlWorkbenchProjectionModes.Perspective
                : ProjectionMode,
            Zoom = uiState.Camera?.Zoom ?? cameraState.Zoom,
            TargetX = uiState.Camera?.TargetX ?? cameraState.TargetX,
            TargetY = uiState.Camera?.TargetY ?? cameraState.TargetY,
            TargetZ = uiState.Camera?.TargetZ ?? cameraState.TargetZ,
            Distance = uiState.Camera?.Distance ?? cameraState.Distance,
            Azimuth = uiState.Camera?.Azimuth ?? cameraState.Azimuth,
            Polar = uiState.Camera?.Polar ?? cameraState.Polar
        };
        LayoutMode = WebGlWorkbenchLayoutModes.Normalize(uiState.LayoutMode);
        NodeSpacingFactor = ClampNodeSpacingFactor(uiState.NodeSpacingFactor);
    }

    public WebGlWorkbenchSurface BuildSurface()
    {
        EnsureInitialized();
        var surface = sceneAdapter.BuildDefinitionScene(
            workingEditor!,
            new ProcessWebGlSceneOptions(
                SelectedTemplateKey,
                ProjectionMode,
                ViewPreset,
                SelectedNodeId,
                LayoutMode: LayoutMode,
                NodeSpacingFactor: NodeSpacingFactor,
                CameraState: cameraState,
                DeterministicMode: true,
                ShowDiagnostics: ShowDiagnostics));
        ApplyNodePositionOverrides(surface);
        return surface;
    }

    private void RecordCommand(string title, string detail)
    {
        commandLog.Insert(0, new ProcessWebGlCommandLogEntry(DateTimeOffset.UtcNow, title, detail));
        if (commandLog.Count > MaxCommandLogEntries)
        {
            commandLog.RemoveRange(MaxCommandLogEntries, commandLog.Count - MaxCommandLogEntries);
        }
    }

    private string ResolveTemplateKey(string? templateKey)
    {
        return Templates.Any(template => string.Equals(template.Key, templateKey, StringComparison.OrdinalIgnoreCase))
            ? Templates.First(template => string.Equals(template.Key, templateKey, StringComparison.OrdinalIgnoreCase)).Key
            : Templates.First().Key;
    }

    private void ApplyNodePositionOverrides(WebGlWorkbenchSurface surface)
    {
        foreach (var node in surface.Nodes)
        {
            if (!nodePositionOverrides.TryGetValue(node.Id, out var position))
            {
                continue;
            }

            node.X = position.X;
            node.Y = position.Y;
            node.Z = position.Z;
        }
    }

    private WebGlWorkbenchCameraState CreateDefaultCameraState()
    {
        return new WebGlWorkbenchCameraState
        {
            ProjectionMode = ProjectionMode,
            Zoom = 1,
            TargetX = 0,
            TargetY = 0,
            TargetZ = 0,
            Distance = 1180,
            Azimuth = -0.72d,
            Polar = 1.08d
        };
    }

    private static double ClampNodeSpacingFactor(double value)
    {
        if (!double.IsFinite(value))
        {
            return 1;
        }

        return Math.Round(Math.Clamp(value, MinNodeSpacingFactor, MaxNodeSpacingFactor), 2, MidpointRounding.AwayFromZero);
    }
}

public sealed record ProcessWebGlCommandLogEntry(
    DateTimeOffset TimestampUtc,
    string Title,
    string Detail);
