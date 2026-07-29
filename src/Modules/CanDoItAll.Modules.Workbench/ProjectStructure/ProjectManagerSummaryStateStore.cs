using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectManagerSummaryViewState(Guid profileId, Guid projectId)
{
    public Guid ProfileId { get; } = profileId;

    public Guid ProjectId { get; } = projectId;

    public ProjectManagerSummaryOptions Options { get; set; } = new();

    public ProjectManagerSummarySnapshot? Snapshot { get; set; }
}

public sealed class ProjectManagerSummaryStateStore
{
    public const int MaximumRetainedStateCount = 32;

    private readonly IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor;
    private readonly int capacity;
    private readonly Lock stateGate = new();
    private readonly Dictionary<ProjectManagerSummaryStateKey, RetainedState> states = [];
    private readonly LinkedList<ProjectManagerSummaryStateKey> recency = [];

    public ProjectManagerSummaryStateStore(
        IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor)
        : this(databaseProfileRuntimeAccessor, MaximumRetainedStateCount)
    {
    }

    internal ProjectManagerSummaryStateStore(
        IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
        int capacity)
    {
        ArgumentNullException.ThrowIfNull(databaseProfileRuntimeAccessor);
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "The retained manager-summary state capacity must be positive.");
        }

        this.databaseProfileRuntimeAccessor = databaseProfileRuntimeAccessor;
        this.capacity = capacity;
    }

    public ProjectManagerSummaryViewState GetOrCreate(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project identifier is required.", nameof(projectId));
        }

        var profileId = databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id;
        if (profileId == Guid.Empty)
        {
            throw new InvalidOperationException("The active database profile identifier is invalid.");
        }

        var key = new ProjectManagerSummaryStateKey(profileId, projectId);
        lock (stateGate)
        {
            if (states.TryGetValue(key, out var retained))
            {
                Touch(retained.RecencyNode);
                return retained.State;
            }

            var state = new ProjectManagerSummaryViewState(profileId, projectId);
            var recencyNode = recency.AddLast(key);
            states.Add(key, new RetainedState(state, recencyNode));
            EvictLeastRecentlyUsedIfNeeded();
            return state;
        }
    }

    private void Touch(LinkedListNode<ProjectManagerSummaryStateKey> recencyNode)
    {
        recency.Remove(recencyNode);
        recency.AddLast(recencyNode);
    }

    private void EvictLeastRecentlyUsedIfNeeded()
    {
        if (states.Count <= capacity)
        {
            return;
        }

        var leastRecentlyUsed = recency.First
            ?? throw new InvalidOperationException(
                "Manager-summary state retention lost its recency index.");
        recency.RemoveFirst();
        if (!states.Remove(leastRecentlyUsed.Value))
        {
            throw new InvalidOperationException(
                "Manager-summary state retention lost its keyed state.");
        }
    }

    private readonly record struct ProjectManagerSummaryStateKey(
        Guid ProfileId,
        Guid ProjectId);

    private sealed record RetainedState(
        ProjectManagerSummaryViewState State,
        LinkedListNode<ProjectManagerSummaryStateKey> RecencyNode);
}
