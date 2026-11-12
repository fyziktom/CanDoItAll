namespace CanDoItAll.Modules.Projects.Pages.Components;

public enum ProjectLifecyclePendingAction { Check, RetryOriginal, ApplyAccepted, CancelPrepared, ClearResolved }

public sealed record ProjectLifecyclePendingIntent(ProjectLifecycleOperation Operation, ProjectLifecyclePendingAction Action);

public sealed record ProjectLifecyclePendingView(ProjectLifecycleOperation Operation, string Message, bool Busy,
    bool CanRetry, bool CanApply, bool CanCancel, bool CanClear);
