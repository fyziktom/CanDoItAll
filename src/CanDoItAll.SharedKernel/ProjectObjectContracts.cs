namespace CanDoItAll.SharedKernel;

public enum ProjectObjectType
{
    ProjectRoot,
    Phase,
    Milestone,
    Repository,
    File,
    Link,
    Connector,
    PromptFlow,
    PromptSession,
    PromptStep,
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

public sealed record ProjectObjectVisualProfile(
    string Shape,
    string AccentColor,
    string Icon,
    string AccentBadge);
