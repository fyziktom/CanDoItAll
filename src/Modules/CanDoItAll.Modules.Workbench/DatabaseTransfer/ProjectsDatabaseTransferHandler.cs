using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectsDatabaseTransferHandler(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    ProjectTransferTargetStateGuard targetStateGuard) : IDatabaseTransferHandler
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
        sourceData.ValidateForImport();
        var sourceCounts = sourceData.Counts;
        if (sourceCounts.Projects == 0)
        {
            return new DatabaseTransferItemResult(Descriptor.Key, Descriptor.Label, false, "The source database has no projects to transfer.", 0);
        }

        if (context.TargetProfile.Profile.Id ==
            profileAccessor.ResolveCurrentProfile().Profile.Id)
        {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "Project transfer requires an inactive target database profile; the running profile was left unchanged.",
                0);
        }

        if (sourceData.HasStorageBindings)
        {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The source contains project media bindings. Database-row transfer cannot copy or restamp their bytes; use project package v2 export/import into an empty inactive profile.",
                0);
        }

        if (sourceData.HasCrossModuleMutations)
        {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The source contains executable cross-module mutation records. Resolve that recovery work before transferring projects.",
                0);
        }

        var targetData = await ProjectTransferDataSet.LoadAsync(
            context.TargetDbContext,
            cancellationToken);
        var targetResidues = await targetStateGuard.FindResiduesAsync(
            context.TargetDbContext,
            cancellationToken);
        if (targetData.HasStorageBindings)
        {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The target contains project media bindings. Database-row transfer refused to orphan their stored bytes; use a new empty inactive target profile.",
                0);
        }

        if (targetData.Counts.Total > 0 || targetResidues.Count > 0)
        {
            var residueDetails = targetResidues.Count == 0
                ? string.Empty
                : $" Related state found: {ProjectTransferTargetStateGuard.Describe(targetResidues)}.";
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "Project transfer requires an inactive target with no project or project-related state so related module data cannot silently attach to imported project ids." +
                residueDetails,
                0);
        }

        await using var transferScope = await SerializableMutationScope.BeginAsync(
            context.TargetDbContext,
            ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey,
            cancellationToken);
        await targetStateGuard.AcquireExclusiveImportLocksAsync(
            context.TargetDbContext,
            cancellationToken);
        var lockedTargetCounts = await ProjectTransferDataSet.CountAsync(
            context.TargetDbContext,
            cancellationToken);
        var lockedTargetResidues = await targetStateGuard.FindResiduesAsync(
            context.TargetDbContext,
            cancellationToken);
        if (lockedTargetCounts.Total > 0 || lockedTargetResidues.Count > 0)
        {
            var residueDetails = lockedTargetResidues.Count == 0
                ? string.Empty
                : $" Related state found: {ProjectTransferTargetStateGuard.Describe(lockedTargetResidues)}.";
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The inactive target acquired project or project-related data before exclusive import locks were established; nothing was replaced." +
                residueDetails,
                0);
        }

        await ProjectTransferDataSet.ClearAsync(context.TargetDbContext, cancellationToken);
        await ProjectTransferDataSet.SaveAsync(context.TargetDbContext, sourceData, cancellationToken);
        await transferScope.CommitAsync(cancellationToken);

        return new DatabaseTransferItemResult(
            Descriptor.Key,
            Descriptor.Label,
            true,
            $"Copied {sourceCounts.Projects} project(s) with their structure workbench data.",
            sourceCounts.Total);
    }
}
