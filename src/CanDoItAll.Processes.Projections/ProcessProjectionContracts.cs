using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Projections;

public sealed record ProcessDefinitionListProjection(
    ProcessDefinitionId Id,
    string Name,
    string Status,
    DateTimeOffset UpdatedAtUtc);
