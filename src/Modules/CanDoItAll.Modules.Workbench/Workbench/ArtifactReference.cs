namespace CanDoItAll.Modules.Workbench;

/// <summary>
/// Reference to the application page or artifact that a project structure node opens, returned by a node command. Open
/// <c>route</c> in the web application to show it.
/// </summary>
/// <param name="Kind">
/// Kind of the referenced artifact: <c>project</c> for the project root node, <c>prompt</c> for a prompt node bound to
/// its Prompt Gallery prompt, <c>test-plan</c> for the Test command, and otherwise usually the node's object type
/// name, for example <c>WorkItem</c> or <c>File</c>.
/// </param>
/// <param name="EntityId">
/// Identifier of the referenced artifact, a GUID, for example the project or the Prompt Gallery prompt; null when the
/// node refers to no separate artifact.
/// </param>
/// <param name="Title">Display title: the node's title, or <c>Test Lab</c> for the Test command.</param>
/// <param name="Route">
/// Relative web application route to open: for example <c>/projects/{projectId}/structure</c> for an ordinary node,
/// the managed file's preview route for a file or media node, or <c>/test-lab?projectId={projectId}</c>.
/// </param>
/// <param name="Description">The node's notes, or a fixed description for the Test command; may be empty.</param>
/// <param name="ProjectId">Identifier of the project the node belongs to.</param>
/// <param name="ArtifactKey">Identifier of the node the reference was resolved for; null for the Test command.</param>
/// <param name="ProjectName">Not set by node commands; always null.</param>
/// <param name="PhaseName">Not set by node commands; always null.</param>
/// <param name="SnapshotJson">Not set by node commands; always null.</param>
/// <param name="TabKind">
/// Workbench tab that shows the reference: <c>project-overview</c> (project root), <c>prompt-detail</c> (prompt
/// nodes), <c>processes</c>, <c>workflows</c>, <c>validation-run</c>, <c>test-plan</c> (test plans and evidence),
/// <c>project-structure</c> or <c>project-calendar</c> (nodes shown on those pages) or <c>page</c>; null for the Test
/// command.
/// </param>
public sealed record ArtifactReference(
    string Kind,
    Guid? EntityId,
    string Title,
    string Route,
    string Description,
    Guid? ProjectId = null,
    string? ArtifactKey = null,
    string? ProjectName = null,
    string? PhaseName = null,
    string? SnapshotJson = null,
    string? TabKind = null);
