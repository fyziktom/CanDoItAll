namespace CanDoItAll.Modules.Processes;

public sealed class ProcessObservationDashboardState
{
    public event Action<ProcessDashboardObservationSnapshot>? DashboardSnapshotChanged;

    public ProcessDashboardObservationSnapshot? DashboardSnapshot { get; private set; }

    public void SetDashboardSnapshot(ProcessDashboardObservationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        DashboardSnapshot = snapshot;
        DashboardSnapshotChanged?.Invoke(snapshot);
    }
}
