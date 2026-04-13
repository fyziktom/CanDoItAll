namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private static readonly ProcessDefinitionListQueryService DefinitionListQueries = new();
    private static readonly ProcessRuntimeReadQueryService RuntimeReadQueries = new();
}
