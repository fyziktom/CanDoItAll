namespace CanDoItAll.Modules.Processes;

public sealed class ProcessRuntimeOptions
{
    public const string SectionName = "Processes:Runtime";

    public bool RequirePostgreSqlForAgentAutomation { get; set; }
}
