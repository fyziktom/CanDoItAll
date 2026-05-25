using System.ComponentModel.DataAnnotations;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessRuntimeOptions
{
    public const string SectionName = "Processes:Runtime";
    public const int DefaultOutboxWorkerConcurrency = 2;
    public const int MaximumOutboxWorkerConcurrency = 8;
    public const int MinimumOutboxBatchSize = 1;
    public const int MaximumOutboxBatchSize = 500;
    public static readonly TimeSpan DefaultStepDispatchClaimLeaseDuration = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan DefaultStepDispatchHeartbeatInterval = TimeSpan.FromSeconds(5);

    public bool RequirePostgreSqlForAgentAutomation { get; set; }

    public bool RecoverActiveRunsOnStartup { get; set; }

    public bool ResumePersistedAutomationDispatchesOnStartup { get; set; }

    [Range(MinimumOutboxBatchSize, MaximumOutboxBatchSize)]
    public int OutboxBatchSize { get; set; } = 20;

    [Range(1, MaximumOutboxWorkerConcurrency)]
    public int OutboxBatchMaxParallelism { get; set; } = DefaultOutboxWorkerConcurrency;

    [Range(1, MaximumOutboxWorkerConcurrency)]
    public int OutboxWorkerMaxConcurrency { get; set; } = DefaultOutboxWorkerConcurrency;

    public TimeSpan StepDispatchClaimLeaseDuration { get; set; } = DefaultStepDispatchClaimLeaseDuration;

    public TimeSpan StepDispatchHeartbeatInterval { get; set; } = DefaultStepDispatchHeartbeatInterval;
}
