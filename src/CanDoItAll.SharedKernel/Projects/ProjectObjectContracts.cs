namespace CanDoItAll.SharedKernel;

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
