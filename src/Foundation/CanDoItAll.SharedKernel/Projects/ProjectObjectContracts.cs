namespace CanDoItAll.SharedKernel;

/// <summary>
/// Kind of object a project structure node represents. Project Structure responses write it as its name, for example
/// <c>"WorkItem"</c>; the request bodies that Project Structure reads itself (structure read, checklist query, node
/// creation, update and type change, asset creation) accept the name in any casing or the integer; other request
/// bodies and other API families use the JSON integer. Values: 0 ProjectRoot (a project node: the project's own root,
/// a subproject or a parent project; created only by the owner), 1 Phase, 2 Milestone, 3 ProjectBlock (a typed block
/// such as feature, delivery or backlog), 4 Meeting, 5 Recording, 6 Transcript, 7 Participant, 8 WorkItem (tasks,
/// issues, feedback, payments, revisions and send items; subtype <c>task</c> is a canonical task), 9 Repository
/// (folder, local or remote repository), 10 File, 11 ImageAsset, 12 VideoAsset, 13 Link (web link), 14 Connector,
/// 15 Script (runtime script), 16 Environment (language runtime), 17 Infrastructure, 18 PromptFlow, 19 PromptSession,
/// 20 PromptStep, 21 ProcessDefinition, 22 ProcessRun, 23 WorkflowDefinition, 24 WorkflowRun, 25 ValidationRun (these
/// five are created only by the owner), 26 TestPlan, 27 TestEvidence, 28 Note, 29 Decision, 30 SecretReference.
/// </summary>
public enum ProjectObjectType
{
    ProjectRoot,
    Phase,
    Milestone,
    ProjectBlock,
    Meeting,
    Recording,
    Transcript,
    Participant,
    WorkItem,
    Repository,
    File,
    ImageAsset,
    VideoAsset,
    Link,
    Connector,
    Script,
    Environment,
    Infrastructure,
    PromptFlow,
    PromptSession,
    PromptStep,
    ProcessDefinition,
    ProcessRun,
    WorkflowDefinition,
    WorkflowRun,
    ValidationRun,
    TestPlan,
    TestEvidence,
    Note,
    Decision,
    SecretReference
}

/// <summary>
/// Meaning of a directed link between two project structure nodes, as a JSON integer. 0 Contains (parent-child
/// containment maintained by the owner; set it through a parent node key or a reparent operation), 1 DependsOn (a task
/// or scheduling prerequisite: the source depends on the target), 2 Uses (the source consumes, references or needs the
/// target), 3 Validates (validation evidence or a validation run validates the target), 4 Tests (a test plan or test
/// evidence tests the target), 5 Blocks (the source blocks the target without being a direct schedule prerequisite),
/// 6 DerivedFrom (the source is a revision or derivation of the target), 7 BelongsTo (a parent relationship maintained
/// by the owner; set it through a parent node key or a reparent operation).
/// </summary>
public enum ProjectObjectLinkKind
{
    Contains,
    DependsOn,
    Uses,
    Validates,
    Tests,
    Blocks,
    DerivedFrom,
    BelongsTo
}

public interface IProjectObject
{
    string NodeKey { get; }

    Guid ProjectId { get; }

    ProjectObjectType ObjectType { get; }

    string Title { get; }

    string Status { get; }
}

public static class ProjectObjectPaletteKeys
{
    public const string Primary = "primary";
    public const string Secondary = "secondary";
    public const string Success = "success";
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Danger = "danger";
    public const string Neutral = "neutral";
}

public sealed record ProjectObjectVisualProfile(
    string Shape,
    string AccentColor,
    string Icon,
    string AccentBadge,
    string PaletteKey = ProjectObjectPaletteKeys.Neutral);
