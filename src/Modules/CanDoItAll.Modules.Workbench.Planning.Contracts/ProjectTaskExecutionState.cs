namespace CanDoItAll.Modules.Workbench;

/// <summary>
/// Recorded execution state of a canonical task. In HTTP request and response bodies it is a JSON integer:
/// 0 Unknown (no state recorded yet), 1 NotStarted, 2 Started, 3 Completed, 4 Cancelled. Inside a node's
/// <c>metadataJson</c> string it appears as the text tokens <c>unknown</c>, <c>notStarted</c>, <c>started</c>,
/// <c>completed</c> and <c>cancelled</c>. Completed and Cancelled are final.
/// </summary>
public enum ProjectTaskExecutionState
{
    Unknown,
    NotStarted,
    Started,
    Completed,
    Cancelled
}

