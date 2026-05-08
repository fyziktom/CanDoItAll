namespace CanDoItAll.Modules.Processes;

public sealed class ProcessRuntimeOptions
{
    public const string SectionName = "Processes:Runtime";

    public bool RequirePostgreSqlForAgentAutomation { get; set; }

    public bool RecoverActiveRunsOnStartup { get; set; }

    public bool ResumePersistedAutomationDispatchesOnStartup { get; set; }

    public int OutboxWorkerMaxConcurrency { get; set; } = 1;
}
