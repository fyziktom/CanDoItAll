using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessCancellationRecoveryOptions {
    internal const string ConfigurationSectionName = "Processes:CancellationRecovery";

    public bool Enabled { get; set; }
    public TimeSpan IdleDelay { get; set; } = TimeSpan.FromSeconds(1);
    public int LegacySeedPageSize { get; set; } = 16;
    public ProcessCancellationRecoverySliceLimits Limits { get; set; } = ProcessCancellationRecoverySliceLimits.Default with { };

    internal static bool IsValid(ProcessCancellationRecoveryOptions options) {
        if (options.IdleDelay < TimeSpan.FromMilliseconds(25) || options.IdleDelay > TimeSpan.FromMinutes(1) ||
                options.LegacySeedPageSize is < 1 or > 64 || options.Limits is null ||
                options.Limits.YieldDelay < TimeSpan.FromMilliseconds(25)) {
            return false;
        }
        try {
            options.Limits.Validate();
            return true;
        } catch (ArgumentOutOfRangeException) {
            return false;
        }
    }
}
