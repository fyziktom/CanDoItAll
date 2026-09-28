using System.ComponentModel;
namespace CanDoItAll.Processes.Projections;

[Description("Classification of canvas node kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionCanvasNodeKind
{
    Step,
    BranchRouter,
    Role,
    Artifact,
    SubprocessBoundary
}

[Description("Classification of canvas edge kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionCanvasEdgeKind
{
    Dependency,
    BranchRoute,
    RoleBinding,
    ArtifactExpectation,
    SubprocessBoundary
}

[Description("Classification of canvas selection kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionCanvasSelectionKind
{
    None,
    Step,
    Route,
    Role,
    Artifact,
    SubprocessBoundary
}

[Description("Classification of canvas toolbox action kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionCanvasToolboxActionKind
{
    Step,
    BranchRouter,
    RoleBinding,
    ArtifactExpectation,
    SubprocessBoundary
}

[Description("Classification of canvas command kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionCanvasCommandKind
{
    AddStep,
    AddBranchRouter,
    AddRoleBinding,
    AddArtifactExpectation,
    AddSubprocessBoundary,
    CloneArtifactReference,
    Recompose,
    MoveNodes,
    CloneRoleReference
}

[Description("Classification of canvas command status. Numeric JSON values are listed by the OpenAPI schema.")]
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

[Description("Classification of canvas port kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionCanvasPortKind
{
    StructuralInput,
    StructuralOutput,
    BranchOutcome,
    RoleBinding,
    ArtifactExpectation,
    SubprocessBoundary
}

[Description("Opaque version of the canvas projection. This is an authoring concurrency token, not an authentication credential.")]
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

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

[Description("Opaque canvas node key represented by its value member. Preserve the value; do not derive it from display text.")]
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

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

[Description("Opaque canvas edge key represented by its value member. Preserve the value; do not derive it from display text.")]
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

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

[Description("Opaque canvas toolbox action key represented by its value member. Preserve the value; do not derive it from display text.")]
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

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

[Description("Canvas dimensions and a human-readable layout explanation.")]
public sealed record ProcessDefinitionCanvasViewportProjection(
    [property: Description("Width measured in canvas coordinate units.")]
    double Width,
    [property: Description("Height measured in canvas coordinate units.")]
    double Height,
    [property: Description("Human-readable explanation of the generated or saved canvas layout.")]
    string LayoutSummary);

[Description("Connection point on a canvas node, positioned relative to that node.")]
public sealed record ProcessDefinitionCanvasPortProjection(
    [property: Description("Identity of the connection point within its canvas node.")]
    string PortKey,
    [property: Description("Classification of this projected authoring item.")]
    ProcessDefinitionCanvasPortKind Kind,
    [property: Description("Human-readable label for this projected authoring item.")]
    string Label,
    [property: Description("Horizontal connection-point offset relative to its canvas node.")]
    double OffsetX,
    [property: Description("Vertical connection-point offset relative to its canvas node.")]
    double OffsetY);

[Description("Canvas node representing a step, role, artifact or subprocess boundary; geometry uses canvas coordinates.")]
public sealed record ProcessDefinitionCanvasEditorNodeProjection(
    [property: Description("Identity of the node within this projected canvas or structure.")]
    ProcessDefinitionCanvasNodeKey NodeKey,
    [property: Description("Classification of this projected authoring item.")]
    ProcessDefinitionCanvasNodeKind Kind,
    [property: Description("Human-readable title of this authoring item.")]
    string Title,
    [property: Description("Secondary display text beneath the item title.")]
    string Subtitle,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Horizontal position in canvas coordinate units.")]
    double X,
    [property: Description("Vertical position in canvas coordinate units.")]
    double Y,
    [property: Description("Width measured in canvas coordinate units.")]
    double Width,
    [property: Description("Height measured in canvas coordinate units.")]
    double Height,
    [property: Description("Semantic presentation tone used to style the canvas item.")]
    string Tone,
    [property: Description("Opaque identity of the definition step referenced by this item, when applicable.")]
    ProcessDefinitionStepKey? StepKey,
    [property: Description("Opaque role identity within the process definition.")]
    ProcessDefinitionRoleKey? RoleKey,
    [property: Description("Identity of the expected artifact associated with this projection.")]
    string? ArtifactKey,
    [property: Description("Short display labels annotating the canvas node.")]
    IReadOnlyList<string> Badges,
    [property: Description("Connection points belonging to the canvas node.")]
    IReadOnlyList<ProcessDefinitionCanvasPortProjection> Ports,
    [property: Description("Behavior category of the definition step.")]
    ProcessDefinitionStepKind? StepKind = null);

public sealed record ProcessDefinitionCanvasNodePosition(
    ProcessDefinitionCanvasNodeKey NodeKey,
    double X,
    double Y);

[Description("Directed connection between canvas nodes, with display styling and backward-route information.")]
public sealed record ProcessDefinitionCanvasEdgeProjection(
    [property: Description("Opaque identity of this canvas connection.")]
    ProcessDefinitionCanvasEdgeKey EdgeKey,
    [property: Description("Classification of this projected authoring item.")]
    ProcessDefinitionCanvasEdgeKind Kind,
    [property: Description("Canvas node key at the source of this directed connection.")]
    ProcessDefinitionCanvasNodeKey FromNodeKey,
    [property: Description("Canvas node key at the destination of this directed connection.")]
    ProcessDefinitionCanvasNodeKey ToNodeKey,
    [property: Description("Human-readable label for this projected authoring item.")]
    string Label,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Semantic presentation tone used to style the canvas item.")]
    string Tone,
    [property: Description("True when the route returns to an earlier step and may require a loop budget.")]
    bool IsBackwardRoute);

[Description("Current canvas selection and its display facts; absent keys mean no corresponding selected item.")]
public sealed record ProcessDefinitionCanvasSelectionProjection(
    [property: Description("Classification of this projected authoring item.")]
    ProcessDefinitionCanvasSelectionKind Kind,
    [property: Description("Identity of the node within this projected canvas or structure.")]
    ProcessDefinitionCanvasNodeKey? NodeKey,
    [property: Description("Opaque identity of this canvas connection.")]
    ProcessDefinitionCanvasEdgeKey? EdgeKey,
    [property: Description("Human-readable title of this authoring item.")]
    string Title,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Human-readable presentation of the selected item key.")]
    string KeyText,
    [property: Description("Display facts describing the selected canvas or template item.")]
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

[Description("Available canvas insertion action and the step kind it would create.")]
public sealed record ProcessDefinitionCanvasToolboxActionProjection(
    [property: Description("Opaque key identifying this template or toolbox action.")]
    ProcessDefinitionCanvasToolboxActionKey ActionKey,
    [property: Description("Classification of this projected authoring item.")]
    ProcessDefinitionCanvasToolboxActionKind Kind,
    [property: Description("Human-readable label for this projected authoring item.")]
    string Label,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Presentation icon name for the command or action.")]
    string Icon,
    [property: Description("Whether the authoring application currently allows this command; this flag does not grant HTTP authority.")]
    bool IsEnabled,
    [property: Description("Human-readable reason a command is unavailable, or null when it is available.")]
    string? DisabledReason,
    [property: Description("Behavior category of the definition step.")]
    ProcessDefinitionStepKind StepKind = ProcessDefinitionStepKind.Unspecified);

[Description("Availability of a canvas command in the authoring projection; this read API does not execute it.")]
public sealed record ProcessDefinitionCanvasCommandProjection(
    [property: Description("Classification of this projected authoring item.")]
    ProcessDefinitionCanvasCommandKind Kind,
    [property: Description("User-facing command label.")]
    string Text,
    [property: Description("Presentation icon name for the command or action.")]
    string Icon,
    [property: Description("Whether the authoring application currently allows this command; this flag does not grant HTTP authority.")]
    bool IsEnabled,
    [property: Description("Human-readable reason a command is unavailable, or null when it is available.")]
    string? DisabledReason);

[Description("Most recent canvas command result with its observed projection version.")]
public sealed record ProcessDefinitionCanvasCommandReceipt(
    [property: Description("Unique identifier of the recorded authoring command receipt.")]
    Guid ReceiptId,
    [property: Description("Authoring command whose outcome this receipt records.")]
    ProcessDefinitionCanvasCommandKind CommandKind,
    [property: Description("Current lifecycle or command outcome of this projection.")]
    ProcessDefinitionCanvasCommandStatus Status,
    [property: Description("Opaque version of this authoring projection; it is not an authentication token.")]
    ProcessDefinitionCanvasVersionToken VersionToken,
    [property: Description("UTC instant when this command result was observed.")]
    DateTimeOffset ObservedAtUtc,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
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

[Description("Definition canvas geometry, connections, selection and available authoring commands.")]
public sealed record ProcessDefinitionCanvasEditorProjection(
    [property: Description("Opaque catalog key of the process definition; preserve it exactly when constructing read URLs.")]
    ProcessDefinitionCatalogItemKey DefinitionKey,
    [property: Description("Opaque version of this authoring projection; it is not an authentication token.")]
    ProcessDefinitionCanvasVersionToken VersionToken,
    [property: Description("Canvas dimensions and layout explanation.")]
    ProcessDefinitionCanvasViewportProjection Viewport,
    [property: Description("Canvas nodes describing steps, roles, artifacts and subprocess boundaries.")]
    IReadOnlyList<ProcessDefinitionCanvasEditorNodeProjection> Nodes,
    [property: Description("Directed canvas connections between node keys.")]
    IReadOnlyList<ProcessDefinitionCanvasEdgeProjection> Edges,
    [property: Description("Available canvas insertion actions in the authoring application.")]
    IReadOnlyList<ProcessDefinitionCanvasToolboxActionProjection> ToolboxActions,
    [property: Description("Current canvas selection and associated display facts.")]
    ProcessDefinitionCanvasSelectionProjection Selection,
    [property: Description("Commands available in the authoring application; these read endpoints do not execute commands.")]
    IReadOnlyList<ProcessDefinitionCanvasCommandProjection> Commands,
    [property: Description("Most recent command receipt, or null when this snapshot has no command receipt.")]
    ProcessDefinitionCanvasCommandReceipt? LastCommandReceipt);
