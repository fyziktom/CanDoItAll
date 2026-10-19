namespace CanDoItAll.Modules.Projects;

public sealed class ProjectLifecycleOriginalIntentRequiredException()
    : InvalidOperationException("Project deletion and hierarchy changes require an already captured original lifecycle submission. A current read cannot replace the caller's original expectation.");
