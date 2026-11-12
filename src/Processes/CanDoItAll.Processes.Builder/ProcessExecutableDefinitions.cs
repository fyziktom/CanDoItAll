namespace CanDoItAll.Processes.Builder;

public sealed record ProcessExecutableDefinitionSource(Guid DatabaseProfileId, Guid ProjectId, Guid ProjectLifetimeId,
    string DefinitionKey, long Revision, Guid? PublicationId, string? PublicationHash, string ContentHash, string ContentJson);

public sealed record ProcessExecutableDefinitionClosure(Guid DatabaseProfileId, Guid ProjectId, Guid ProjectLifetimeId,
    string DefinitionKey, IReadOnlyDictionary<string, ProcessExecutableDefinitionSource> Definitions);
