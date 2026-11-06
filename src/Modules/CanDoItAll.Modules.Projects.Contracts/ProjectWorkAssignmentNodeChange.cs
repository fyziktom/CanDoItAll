namespace CanDoItAll.Modules.Projects;

public sealed record ProjectWorkAssignmentNodeChange(Guid DatabaseProfileId, Guid ProjectId, string NodeKey, string Title);
