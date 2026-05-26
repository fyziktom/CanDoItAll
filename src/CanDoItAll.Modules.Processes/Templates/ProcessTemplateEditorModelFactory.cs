using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessTemplateEditorModelFactory
{
    public static ProcessRoleEditorModel CreateRoleFromResource(
        ProcessTemplateRoleResource resource,
        Guid id,
        string key,
        string displayName,
        string preferredExecutorKindFallback,
        int defaultAllocationPercentFallback,
        double canvasX = 0,
        double canvasY = 0)
    {
        ArgumentNullException.ThrowIfNull(resource);

        return new ProcessRoleEditorModel
        {
            Id = id,
            Key = key,
            DisplayName = displayName,
            Purpose = resource.Purpose,
            StaffingIntent = resource.StaffingIntent,
            PreferredExecutorKind = string.IsNullOrWhiteSpace(resource.PreferredExecutorKind)
                ? preferredExecutorKindFallback
                : resource.PreferredExecutorKind,
            PreferredProjectAssignmentRole = EnumValueParser.ParseNullable<ProjectPartyAssignmentRole>(resource.PreferredProjectAssignmentRole),
            IsRequired = resource.IsRequired,
            AllowsFallback = resource.AllowsFallback,
            RequiresExplicitApproval = resource.RequiresExplicitApproval,
            DefaultAllocationPercent = resource.DefaultAllocationPercent > 0
                ? resource.DefaultAllocationPercent
                : Math.Max(0, defaultAllocationPercentFallback),
            RoleTemplateSourceKey = string.IsNullOrWhiteSpace(resource.RoleTemplateSourceKey)
                ? resource.Key
                : resource.RoleTemplateSourceKey,
            RoleTemplateSnapshotName = resource.RoleTemplateSnapshotName,
            SnapshotSummary = ProcessTemplateRoleSnapshotSummaryBuilder.Build(resource),
            CanvasX = canvasX,
            CanvasY = canvasY
        };
    }

    public static ProcessRoleEditorModel CreateRoleFromUsage(
        ProcessTemplateRoleUsage usage,
        ProcessTemplateRoleResource? resource,
        Guid id)
    {
        ArgumentNullException.ThrowIfNull(usage);

        return new ProcessRoleEditorModel
        {
            Id = id,
            Key = usage.Key,
            DisplayName = FirstNonEmpty(usage.DisplayName, resource?.DisplayName),
            Purpose = FirstNonEmpty(usage.Purpose, resource?.Purpose),
            StaffingIntent = FirstNonEmpty(usage.StaffingIntent, resource?.StaffingIntent),
            PreferredExecutorKind = FirstNonEmpty(usage.PreferredExecutorKind, resource?.PreferredExecutorKind),
            PreferredProjectAssignmentRole = EnumValueParser.ParseNullable<ProjectPartyAssignmentRole>(
                FirstNonEmpty(usage.PreferredProjectAssignmentRole, resource?.PreferredProjectAssignmentRole)),
            IsRequired = usage.IsRequired,
            AllowsFallback = usage.AllowsFallback,
            RequiresExplicitApproval = usage.RequiresExplicitApproval,
            DefaultAllocationPercent = usage.DefaultAllocationPercent > 0
                ? usage.DefaultAllocationPercent
                : Math.Max(0, resource?.DefaultAllocationPercent ?? 100),
            RoleTemplateSourceKey = resource is null
                ? string.Empty
                : string.IsNullOrWhiteSpace(resource.RoleTemplateSourceKey)
                    ? resource.Key
                    : resource.RoleTemplateSourceKey,
            RoleTemplateSnapshotName = resource?.RoleTemplateSnapshotName ?? string.Empty,
            SnapshotSummary = ProcessTemplateRoleSnapshotSummaryBuilder.Build(resource),
            CanvasX = usage.CanvasX,
            CanvasY = usage.CanvasY
        };
    }

    public static ProcessArtifactExpectationEditorModel CreateArtifactExpectationFromTemplate(
        ProcessTemplateArtifactExpectation template,
        ProcessTemplateArtifactResource? resource,
        Guid id)
    {
        ArgumentNullException.ThrowIfNull(template);

        return new ProcessArtifactExpectationEditorModel
        {
            Id = id,
            ArtifactKind = EnumValueParser.ParseOrDefault(
                FirstNonEmpty(template.ArtifactKind, resource?.ArtifactKind),
                ProcessArtifactKind.Evidence),
            Title = FirstNonEmpty(
                template.Title,
                resource?.DisplayName,
                template.TemplateKey,
                template.Key),
            IsRequired = template.IsRequired,
            TrustRequirement = EnumValueParser.ParseOrDefault(
                FirstNonEmpty(template.TrustRequirement, resource?.DefaultTrustRequirement),
                ProcessArtifactTrustRequirement.ReviewRequired),
            SensitivityLevel = EnumValueParser.ParseOrDefault(
                FirstNonEmpty(template.SensitivityLevel, resource?.DefaultSensitivityLevel),
                ProcessSensitivityLevel.Internal),
            RetentionDays = template.RetentionDays > 0
                ? template.RetentionDays
                : resource?.DefaultRetentionDays ?? 90,
            AllowedFutureUsageSummary = FirstNonEmpty(
                template.AllowedFutureUsageSummary,
                resource?.AllowedFutureUsageSummary),
            ValidationRequirementSummary = FirstNonEmpty(
                template.ValidationRequirementSummary,
                resource?.ValidationRequirementSummary),
            WorkflowOutputId = template.WorkflowOutputId.Trim(),
            WorkflowOutputName = template.WorkflowOutputName.Trim(),
            WorkflowOutputKind = EnumValueParser.ParseNullable<WorkflowArtifactKind>(template.WorkflowOutputKind),
            SubprocessChildArtifactExpectationId = NormalizeGuid(template.SubprocessChildArtifactExpectationId)
        };
    }

    public static ProcessArtifactExpectationEditorModel CreateArtifactExpectationFromResource(
        ProcessTemplateArtifactResource resource,
        Guid id,
        bool isRequired)
    {
        ArgumentNullException.ThrowIfNull(resource);

        return new ProcessArtifactExpectationEditorModel
        {
            Id = id,
            ArtifactKind = EnumValueParser.ParseOrDefault(resource.ArtifactKind, ProcessArtifactKind.Evidence),
            Title = resource.DisplayName,
            IsRequired = isRequired,
            TrustRequirement = EnumValueParser.ParseOrDefault(
                resource.DefaultTrustRequirement,
                ProcessArtifactTrustRequirement.ReviewRequired),
            SensitivityLevel = EnumValueParser.ParseOrDefault(
                resource.DefaultSensitivityLevel,
                ProcessSensitivityLevel.Internal),
            RetentionDays = resource.DefaultRetentionDays > 0
                ? resource.DefaultRetentionDays
                : 90,
            AllowedFutureUsageSummary = resource.AllowedFutureUsageSummary,
            ValidationRequirementSummary = resource.ValidationRequirementSummary
        };
    }

    private static Guid? NormalizeGuid(Guid? value)
    {
        return value.HasValue && value.Value != Guid.Empty
            ? value.Value
            : null;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}
