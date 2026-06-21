namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRuntimeDispatchQueueOptions
{
    public const string ConfigurationSectionName = "Processes:RuntimeDispatchQueue";

    public bool EnableRecovery { get; set; } = true;

    public int ImmediateQueueCapacity { get; set; } = 4096;

    public int RecoveryQueueCapacity { get; set; } = 4096;

    public TimeSpan ActiveClaimWithoutExecutionRunStaleAfter { get; set; } = TimeSpan.FromMinutes(2);
}
