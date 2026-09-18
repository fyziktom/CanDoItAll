using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Resources;

public sealed record ResourceProjectionFact(
    Guid Id,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Name,
    string LocationOrIdentifier,
    ResourceValidationStatus ValidationStatus,
    string Description,
    DateTimeOffset CreatedAtUtc);

public sealed record ResourceProjectionScopeFact(Guid ProjectId, ProjectObjectType ObjectType, string ObjectSubtype);
