namespace CanDoItAll.Modules.Workbench;

internal static class ProjectNodeTransitionHistory
{
    public static ProjectNodeLifecycleEventRecord CaptureReclassification(
        Guid projectId,
        ProjectObjectRecord sourceNode,
        ProjectNodeKindDescriptor sourceDescriptor,
        ProjectObjectRecord targetNode,
        ProjectNodeKindDescriptor targetDescriptor,
        DateTimeOffset occurredAtUtc)
    {
        return ProjectNodeLifecycleHistory.CaptureReclassification(
            projectId,
            sourceNode,
            sourceDescriptor,
            targetNode,
            targetDescriptor,
            occurredAtUtc);
    }
}
