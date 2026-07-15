namespace CanDoItAll.Processes.Projections;

public enum ProcessDefinitionCanvasNodeKind
{
    Step,
    BranchRouter,
    Role,
    Artifact,
    SubprocessBoundary
}

public enum ProcessDefinitionCanvasEdgeKind
{
    Dependency,
    BranchRoute,
    RoleBinding,
    ArtifactExpectation,
    SubprocessBoundary
}

public enum ProcessDefinitionCanvasSelectionKind
{
    None,
    Step,
    Route,
    Role,
    Artifact,
    SubprocessBoundary
}

public enum ProcessDefinitionCanvasToolboxActionKind
{
    Step,
    BranchRouter,
    RoleBinding,
    ArtifactExpectation,
    SubprocessBoundary
}

public enum ProcessDefinitionCanvasCommandKind
{
    AddStep,
    AddBranchRouter,
    AddRoleBinding,
    AddArtifactExpectation,
    AddSubprocessBoundary,
    CloneArtifactReference,
    Recompose,
    MoveNodes
}

public enum ProcessDefinitionCanvasCommandStatus
{
    Accepted,
    Rejected
}

public enum ProcessDefinitionCanvasRecompositionMode
{
    PreserveProjection,
    BalancedFlow
}

public enum ProcessDefinitionCanvasPortKind
{
    StructuralInput,
    StructuralOutput,
    BranchOutcome,
    RoleBinding,
    ArtifactExpectation,
    SubprocessBoundary
}

public readonly record struct ProcessDefinitionCanvasVersionToken
{
    public ProcessDefinitionCanvasVersionToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Definition canvas version token is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessDefinitionCanvasNodeKey
{
    public ProcessDefinitionCanvasNodeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Definition canvas node key is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessDefinitionCanvasEdgeKey
{
    public ProcessDefinitionCanvasEdgeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Definition canvas edge key is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessDefinitionCanvasToolboxActionKey
{
    public ProcessDefinitionCanvasToolboxActionKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Definition canvas toolbox action key is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record ProcessDefinitionCanvasViewportProjection(
    double Width,
    double Height,
    string LayoutSummary);

public sealed record ProcessDefinitionCanvasPortProjection(
    string PortKey,
    ProcessDefinitionCanvasPortKind Kind,
    string Label,
    double OffsetX,
    double OffsetY);

public sealed record ProcessDefinitionCanvasEditorNodeProjection(
    ProcessDefinitionCanvasNodeKey NodeKey,
    ProcessDefinitionCanvasNodeKind Kind,
    string Title,
    string Subtitle,
    string Summary,
    double X,
    double Y,
    double Width,
    double Height,
    string Tone,
    ProcessDefinitionStepKey? StepKey,
    ProcessDefinitionRoleKey? RoleKey,
    string? ArtifactKey,
    IReadOnlyList<string> Badges,
    IReadOnlyList<ProcessDefinitionCanvasPortProjection> Ports,
    ProcessDefinitionStepKind? StepKind = null);

public sealed record ProcessDefinitionCanvasNodePosition(
    ProcessDefinitionCanvasNodeKey NodeKey,
    double X,
    double Y);

public sealed record ProcessDefinitionCanvasEdgeProjection(
    ProcessDefinitionCanvasEdgeKey EdgeKey,
    ProcessDefinitionCanvasEdgeKind Kind,
    ProcessDefinitionCanvasNodeKey FromNodeKey,
    ProcessDefinitionCanvasNodeKey ToNodeKey,
    string Label,
    string Summary,
    string Tone,
    bool IsBackwardRoute);

public sealed record ProcessDefinitionCanvasSelectionProjection(
    ProcessDefinitionCanvasSelectionKind Kind,
    ProcessDefinitionCanvasNodeKey? NodeKey,
    ProcessDefinitionCanvasEdgeKey? EdgeKey,
    string Title,
    string Summary,
    string KeyText,
    IReadOnlyList<string> Facts)
{
    public static ProcessDefinitionCanvasSelectionProjection None { get; } = new(
        ProcessDefinitionCanvasSelectionKind.None,
        NodeKey: null,
        EdgeKey: null,
        "No canvas selection",
        "Select a node, route, role, artifact, or subprocess boundary.",
        string.Empty,
        []);
}

public sealed record ProcessDefinitionCanvasToolboxActionProjection(
    ProcessDefinitionCanvasToolboxActionKey ActionKey,
    ProcessDefinitionCanvasToolboxActionKind Kind,
    string Label,
    string Summary,
    string Icon,
    bool IsEnabled,
    string? DisabledReason,
    ProcessDefinitionStepKind StepKind = ProcessDefinitionStepKind.Unspecified);

public sealed record ProcessDefinitionCanvasCommandProjection(
    ProcessDefinitionCanvasCommandKind Kind,
    string Text,
    string Icon,
    bool IsEnabled,
    string? DisabledReason);

public sealed record ProcessDefinitionCanvasCommandReceipt(
    Guid ReceiptId,
    ProcessDefinitionCanvasCommandKind CommandKind,
    ProcessDefinitionCanvasCommandStatus Status,
    ProcessDefinitionCanvasVersionToken VersionToken,
    DateTimeOffset ObservedAtUtc,
    string Summary);

public sealed record ProcessDefinitionCanvasCommand(
    ProcessWorkspaceShellScope Scope,
    ProcessDefinitionCatalogItemKey DefinitionKey,
    ProcessDefinitionCanvasCommandKind CommandKind,
    ProcessDefinitionCanvasVersionToken? ExpectedVersionToken,
    ProcessDefinitionCanvasToolboxActionKey? ToolboxActionKey,
    ProcessDefinitionCanvasNodeKey? SelectedNodeKey,
    ProcessDefinitionCanvasEdgeKey? SelectedEdgeKey,
    ProcessDefinitionCanvasRecompositionMode RecompositionMode,
    IReadOnlyList<ProcessDefinitionCanvasNodePosition>? NodePositions = null);

public sealed record ProcessDefinitionCanvasCommandResult(
    ProcessDefinitionCanvasCommandReceipt Receipt,
    ProcessDefinitionCanvasEditorProjection Projection);

public sealed record ProcessDefinitionCanvasEditorProjection(
    ProcessDefinitionCatalogItemKey DefinitionKey,
    ProcessDefinitionCanvasVersionToken VersionToken,
    ProcessDefinitionCanvasViewportProjection Viewport,
    IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> Nodes,
    IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> Edges,
    IReadOnlyList<ProcessDefinitionCanvasToolboxActionProjection> ToolboxActions,
    ProcessDefinitionCanvasSelectionProjection Selection,
    IReadOnlyList<ProcessDefinitionCanvasCommandProjection> Commands,
    ProcessDefinitionCanvasCommandReceipt? LastCommandReceipt);
