namespace CanDoItAll.Workbench.Content.UI.Analysis;

public enum ContentStorageDisposition { RetainManagedFiles, DeleteOwnedManagedFiles }

public sealed record ContentDeleteConfirmation(string Title, string ImpactCopy, int SelectedNodeCount,
    int DescendantCount, int ManagedAttachmentCount, string? Failure = null) {
    public bool IsBulk => SelectedNodeCount > 1;
    public bool DeletesMultipleNodes => IsBulk || DescendantCount > 0;
    public string AriaLabel => IsBulk ? $"Delete {SelectedNodeCount} selected nodes" : $"Delete {Title}";
}

public sealed record ContentDeleteActions(Func<ContentStorageDisposition, Task> Confirm, Func<Task> Cancel);
