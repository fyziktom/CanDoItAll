using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public sealed class ProjectStructureMcpDatabaseTransferHandler : IDatabaseTransferHandler
{
    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        "project-structure-mcp-token",
        "Project structure MCP token",
        "Copies ProjectStructure MCP central settings, agent profiles, encrypted tokens, and project overrides.",
        SortOrder: 10,
        IsSensitive: true);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        await WorkspaceSchemaInitializer.EnsureAsync(context.SourceDbContext, cancellationToken);
        await WorkspaceSchemaInitializer.EnsureAsync(context.TargetDbContext, cancellationToken);

        var sourceSettings = await context.SourceDbContext.Set<ProjectStructureAgentWorkspaceSettingsRecord>()
            .CountAsync(cancellationToken);
        var sourceProfiles = await context.SourceDbContext.Set<ProjectStructureAgentProfileRecord>()
            .CountAsync(cancellationToken);
        var sourceOverrides = await context.SourceDbContext.Set<ProjectStructureAgentProjectOverrideRecord>()
            .CountAsync(cancellationToken);
        var targetRecords = await CountTargetRecordsAsync(context, cancellationToken);

        var sourceRecords = sourceSettings + sourceProfiles + sourceOverrides;
        return new DatabaseTransferItemPreview(
            Descriptor,
            sourceRecords > 0,
            sourceProfiles == 1
                ? "1 ProjectStructure agent profile is available."
                : $"{sourceProfiles} ProjectStructure agent profiles are available.",
            sourceProfiles == 0 ? "The source database does not contain a ProjectStructure MCP token profile." : null,
            sourceRecords,
            targetRecords);
    }

    public async Task<DatabaseTransferItemResult> TransferAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        await WorkspaceSchemaInitializer.EnsureAsync(context.SourceDbContext, cancellationToken);
        await WorkspaceSchemaInitializer.EnsureAsync(context.TargetDbContext, cancellationToken);

        var settings = await context.SourceDbContext.Set<ProjectStructureAgentWorkspaceSettingsRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var profiles = await context.SourceDbContext.Set<ProjectStructureAgentProfileRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var overrides = await context.SourceDbContext.Set<ProjectStructureAgentProjectOverrideRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (settings.Count + profiles.Count + overrides.Count == 0)
        {
            return new DatabaseTransferItemResult(Descriptor.Key, Descriptor.Label, false, "The source database has no ProjectStructure MCP settings to transfer.", 0);
        }

        var targetOverrides = await context.TargetDbContext.Set<ProjectStructureAgentProjectOverrideRecord>()
            .ToListAsync(cancellationToken);
        var targetProfiles = await context.TargetDbContext.Set<ProjectStructureAgentProfileRecord>()
            .ToListAsync(cancellationToken);
        var targetSettings = await context.TargetDbContext.Set<ProjectStructureAgentWorkspaceSettingsRecord>()
            .ToListAsync(cancellationToken);

        context.TargetDbContext.RemoveRange(targetOverrides);
        context.TargetDbContext.RemoveRange(targetProfiles);
        context.TargetDbContext.RemoveRange(targetSettings);
        await context.TargetDbContext.SaveChangesAsync(cancellationToken);

        await context.TargetDbContext.Set<ProjectStructureAgentWorkspaceSettingsRecord>().AddRangeAsync(settings, cancellationToken);
        await context.TargetDbContext.Set<ProjectStructureAgentProfileRecord>().AddRangeAsync(profiles, cancellationToken);
        await context.TargetDbContext.Set<ProjectStructureAgentProjectOverrideRecord>().AddRangeAsync(overrides, cancellationToken);
        await context.TargetDbContext.SaveChangesAsync(cancellationToken);

        var copied = settings.Count + profiles.Count + overrides.Count;
        return new DatabaseTransferItemResult(
            Descriptor.Key,
            Descriptor.Label,
            true,
            $"Copied {profiles.Count} ProjectStructure profile(s), including encrypted token data.",
            copied);
    }

    private static async Task<int> CountTargetRecordsAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken)
    {
        var settings = await context.TargetDbContext.Set<ProjectStructureAgentWorkspaceSettingsRecord>()
            .CountAsync(cancellationToken);
        var profiles = await context.TargetDbContext.Set<ProjectStructureAgentProfileRecord>()
            .CountAsync(cancellationToken);
        var overrides = await context.TargetDbContext.Set<ProjectStructureAgentProjectOverrideRecord>()
            .CountAsync(cancellationToken);

        return settings + profiles + overrides;
    }
}
