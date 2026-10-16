namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDefinitionOperationOptions {
    public const string SectionName = "Projects:DefinitionOperations";
    public static readonly TimeSpan MaintenanceInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan MaintenanceBudget = TimeSpan.FromSeconds(2);
    public const int PurgeBatchCount = 128;
    public const int PurgeBatchBytes = 16 * 1024 * 1024;
    public const int MaximumMaintenanceBatches = 64;

    public TimeSpan AdmissionWindow { get; set; } = TimeSpan.FromDays(1);
    public TimeSpan ReplayRetention { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan MutationDeadline { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan StatementDeadline { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan LockDeadline { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxPhaseCount { get; set; } = 1024;
    public int MaxOptionCount { get; set; } = 1024;
    public int MaxFieldUtf8Bytes { get; set; } = 65_536;
    public int MaxRawRequestBytes { get; set; } = 2_097_152;
    public int MaxCanonicalCommandBytes { get; set; } = 1_048_576;
    public int MaxAcceptedResultBytes { get; set; } = 2_097_152;

    public bool IsValid() {
        var maximum = ProjectDefinitionLimits.VersionOneMaximum;
        return AdmissionWindow >= TimeSpan.FromSeconds(1) && AdmissionWindow <= TimeSpan.FromDays(30) &&
            ReplayRetention >= TimeSpan.FromSeconds(1) && ReplayRetention <= TimeSpan.FromDays(365) &&
            MutationDeadline >= TimeSpan.FromSeconds(1) && MutationDeadline <= TimeSpan.FromMinutes(1) &&
            StatementDeadline >= TimeSpan.FromMilliseconds(1) && StatementDeadline <= MutationDeadline &&
            LockDeadline >= TimeSpan.FromMilliseconds(1) && LockDeadline <= StatementDeadline &&
            InRange(MaxPhaseCount, maximum.MaxPhaseCount) && InRange(MaxOptionCount, maximum.MaxOptionCount) &&
            InRange(MaxFieldUtf8Bytes, maximum.MaxFieldUtf8Bytes) && InRange(MaxRawRequestBytes, maximum.MaxRawRequestBytes) &&
            InRange(MaxCanonicalCommandBytes, maximum.MaxCanonicalCommandBytes) && InRange(MaxAcceptedResultBytes, maximum.MaxAcceptedResultBytes);
    }

    public ProjectDefinitionLimits GetLimits() {
        if (!IsValid()) {
            throw new InvalidOperationException("Project definition operation options exceed the supported version-one limits or deadlines.");
        }
        return new(MaxPhaseCount, MaxOptionCount, MaxFieldUtf8Bytes, MaxRawRequestBytes, MaxCanonicalCommandBytes, MaxAcceptedResultBytes);
    }

    private static bool InRange(int value, int maximum) => value > 0 && value <= maximum;
}
