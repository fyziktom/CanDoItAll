using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectsDatabaseTransferHandler(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    ProjectTransferTargetStateGuard targetStateGuard, DatabaseTransferOperationRunner operations, ProjectsProfileTransferStore projects) : IDatabaseTransferHandler {
    private readonly ProjectTransferStore data = new(operations, projects);

    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        "projects",
        "Projects",
        "Copies all projects, project hierarchy, and project workbench structure data.",
        SortOrder: 25);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(
        DatabaseTransferOperation context,
        CancellationToken cancellationToken = default) {
        var sourceCounts = await operations.RunIndependentAsync(context.SourceProfile, data.CountAsync, cancellationToken);
        var targetCounts = await operations.RunIndependentAsync(context.TargetProfile, data.CountAsync, cancellationToken);

        return new DatabaseTransferItemPreview(
            Descriptor,
            sourceCounts.Total > 0,
            $"{sourceCounts.Projects} project(s), {sourceCounts.Objects} structure object(s), and {sourceCounts.ViewStates} view state record(s) are available.",
            sourceCounts.Total == 0 ? "The source database does not contain projects or retained project history." : null,
            sourceCounts.Total,
            targetCounts.Total);
    }

    public async Task<DatabaseTransferItemResult> TransferAsync(
        DatabaseTransferOperation context,
        CancellationToken cancellationToken = default) {
        CoordinatedDatabaseTransaction.RequireDistinctPhysicalDatabases(context.SourceProfile, context.TargetProfile);
        var sourceData = await operations.RunSerializableAsync(context.SourceProfile,
            [ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey], async (session, token) => {
                var loaded = await data.LoadAsync(session, token);
                loaded.ValidateForImport();
                return loaded;
            }, cancellationToken);
        var sourceCounts = sourceData.Counts;
        if (sourceCounts.Total == 0) {
            return new DatabaseTransferItemResult(Descriptor.Key, Descriptor.Label, false, "The source database has no projects or retained project history to transfer.", 0);
        }

        if (context.TargetProfile.Profile.Id ==
            profileAccessor.ResolveCurrentProfile().Profile.Id) {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "Project transfer requires an inactive target database profile; the running profile was left unchanged.",
                0);
        }

        if (sourceData.HasStorageBindings) {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The source contains project media bindings. Database-row transfer cannot copy or restamp their bytes; use project package export/import into an empty inactive profile.",
                0);
        }

        if (sourceData.HasCrossModuleMutations) {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The source contains executable cross-module mutation records. Resolve that recovery work before transferring projects.",
                0);
        }

        var targetPreflight = await operations.RunIndependentAsync(context.TargetProfile, async (session, token) =>
            (Data: await data.LoadAsync(session, token), Residues: await targetStateGuard.FindPreflightResiduesAsync(session, token)), cancellationToken);
        var targetData = targetPreflight.Data;
        var targetResidues = targetPreflight.Residues;
        if (targetData.HasStorageBindings) {
            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                false,
                "The target contains project media bindings. Database-row transfer refused to orphan their stored bytes; use a new empty inactive target profile.",
                0);
        }

        if (targetData.Counts.Total > 0 || targetResidues.Count > 0) {
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

        sourceData.PrepareForTargetImport(context.SourceProfile.Profile.Id, Guid.NewGuid());
        return await targetStateGuard.RunLockedImportAsync(context.TargetProfile, async (session, token) => {
            if (context.TargetProfile.Profile.Id == profileAccessor.ResolveCurrentProfile().Profile.Id) {
                throw new InvalidOperationException("Project transfer requires an inactive target database profile; the running profile was left unchanged.");
            }
            var lockedTargetCounts = await data.CountAsync(
                session,
                cancellationToken);
            var lockedTargetResidues = await targetStateGuard.FindLockedResiduesAsync(
                session,
                cancellationToken);
            if (lockedTargetCounts.Total > 0 || lockedTargetResidues.Count > 0) {
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

            await data.ClearAsync(session, cancellationToken);
            await data.SaveAsync(session, sourceData, cancellationToken);

            return new DatabaseTransferItemResult(
                Descriptor.Key,
                Descriptor.Label,
                true,
                $"Copied {sourceCounts.Projects} project(s) with their structure workbench data and retained history. New target admissions are required to execute imported history.",
                sourceCounts.Total);
        }, cancellationToken);
    }
}
