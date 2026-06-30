using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectsDatabaseTransferHandler : IDatabaseTransferHandler
{
    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        "projects",
        "Projects",
        "Copies all projects, project hierarchy, and project workbench structure data.",
        SortOrder: 25);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        var sourceCounts = await ProjectTransferDataSet.CountAsync(context.SourceDbContext, cancellationToken);
        var targetCounts = await ProjectTransferDataSet.CountAsync(context.TargetDbContext, cancellationToken);

        return new DatabaseTransferItemPreview(
            Descriptor,
            sourceCounts.Projects > 0,
            $"{sourceCounts.Projects} project(s), {sourceCounts.Objects} structure object(s), and {sourceCounts.ViewStates} view state record(s) are available.",
            sourceCounts.Projects == 0 ? "The source database does not contain projects." : null,
            sourceCounts.Total,
            targetCounts.Total);
    }

    public async Task<DatabaseTransferItemResult> TransferAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        var sourceData = await ProjectTransferDataSet.LoadAsync(context.SourceDbContext, cancellationToken);
        var sourceCounts = sourceData.Counts;
        if (sourceCounts.Projects == 0)
        {
            return new DatabaseTransferItemResult(Descriptor.Key, Descriptor.Label, false, "The source database has no projects to transfer.", 0);
        }

        if (!context.ReplaceExisting)
        {
            var targetCounts = await ProjectTransferDataSet.CountAsync(context.TargetDbContext, cancellationToken);
            if (targetCounts.Total > 0)
            {
                return new DatabaseTransferItemResult(
                    Descriptor.Key,
                    Descriptor.Label,
                    false,
                    "The target database already has project data. Enable replacement before transferring projects.",
                    0);
            }
        }

        await ProjectTransferDataSet.ClearAsync(context.TargetDbContext, cancellationToken);
        await ProjectTransferDataSet.SaveAsync(context.TargetDbContext, sourceData, cancellationToken);

        return new DatabaseTransferItemResult(
            Descriptor.Key,
            Descriptor.Label,
            true,
            $"Copied {sourceCounts.Projects} project(s) with their structure workbench data.",
            sourceCounts.Total);
    }
}
