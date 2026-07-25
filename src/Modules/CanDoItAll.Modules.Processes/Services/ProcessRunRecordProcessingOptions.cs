namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRunRecordProcessingOptions
{
    public const string ConfigurationSectionName = "Processes:RunRecords";

    public bool Enabled { get; init; } = true;

    public int BatchSize { get; init; } = 8;

    public int MaximumAttempts { get; init; } = 5;

    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(10);

    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan RetryMaximumDelay { get; init; } = TimeSpan.FromMinutes(30);

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    public static bool IsValid(ProcessRunRecordProcessingOptions options)
    {
        return options.BatchSize is >= 1 and <= 100 &&
               options.MaximumAttempts is >= 1 and <= 20 &&
               options.LeaseDuration is { } leaseDuration &&
               leaseDuration >= TimeSpan.FromSeconds(30) &&
               leaseDuration <= TimeSpan.FromHours(1) &&
               options.RetryBaseDelay > TimeSpan.Zero &&
               options.RetryMaximumDelay >= options.RetryBaseDelay &&
               options.PollInterval >= TimeSpan.FromMilliseconds(100) &&
               options.PollInterval <= TimeSpan.FromMinutes(1);
    }
}
