namespace CanDoItAll.Modules.Processes;

public sealed class ProcessRuntimeOptions
{
    public const string SectionName = "Processes:Runtime";
    public const int DefaultOutboxWorkerConcurrency = 2;
    public const int MaximumOutboxWorkerConcurrency = 8;

    public bool RequirePostgreSqlForAgentAutomation { get; set; }

    public bool RecoverActiveRunsOnStartup { get; set; }

    public bool ResumePersistedAutomationDispatchesOnStartup { get; set; }

    public int OutboxBatchSize { get; set; } = 20;

    public int OutboxBatchMaxParallelism { get; set; } = DefaultOutboxWorkerConcurrency;

    public int OutboxWorkerMaxConcurrency { get; set; } = DefaultOutboxWorkerConcurrency;
}
