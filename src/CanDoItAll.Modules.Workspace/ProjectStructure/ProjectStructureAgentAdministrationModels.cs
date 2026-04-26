using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workspace;

[Flags]
public enum ProjectStructureAgentCapability
{
    None = 0,
    ReadStructure = 1,
    MutateStructure = 2,
    ImportStructure = 4,
    ReadKnowledge = 8,
    ManageLeases = 16,
    All = ReadStructure | MutateStructure | ImportStructure | ReadKnowledge | ManageLeases
}

public sealed class ProjectStructureAgentWorkspaceSettingsRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string CentralBaseUrl { get; set; } = "https://localhost:7271";

    public string InstallScriptPath { get; set; } = @"tools\Install-CanDoItAllProjectStructureMcp.ps1";

    public string SetupReadmePath { get; set; } = @"docs\project-structure-mcp-setup.md";

    public int DefaultAutoApproveMinutes { get; set; }

    public int DefaultApprovalRequiredMinutes { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class ProjectStructureAgentWorkspaceSettingsRecordConfiguration : IEntityTypeConfiguration<ProjectStructureAgentWorkspaceSettingsRecord>
{
    public void Configure(EntityTypeBuilder<ProjectStructureAgentWorkspaceSettingsRecord> builder)
    {
        builder.ToTable("Workspace_ProjectStructureAgentSettings");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.CentralBaseUrl).HasMaxLength(500).IsRequired();
        builder.Property(item => item.InstallScriptPath).HasMaxLength(260).IsRequired();
        builder.Property(item => item.SetupReadmePath).HasMaxLength(260).IsRequired();
    }
}

public sealed class ProjectStructureAgentProfileRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string AccessTokenCipherText { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public ProjectStructureAgentCapability CapabilityMask { get; set; } = ProjectStructureAgentCapability.All;

    public int AutoApproveMinutes { get; set; }

    public int ApprovalRequiredMinutes { get; set; }

    public bool RequireApprovalForAllMutations { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class ProjectStructureAgentProfileRecordConfiguration : IEntityTypeConfiguration<ProjectStructureAgentProfileRecord>
{
    public void Configure(EntityTypeBuilder<ProjectStructureAgentProfileRecord> builder)
    {
        builder.ToTable("Workspace_ProjectStructureAgentProfiles");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Description).HasColumnType("TEXT");
        builder.Property(item => item.AccessTokenCipherText).HasColumnType("TEXT").IsRequired();
        builder.Property(item => item.Notes).HasColumnType("TEXT");
    }
}

public sealed class ProjectStructureAgentProjectOverrideRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProfileId { get; set; }

    public Guid ProjectId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public ProjectStructureAgentCapability CapabilityMask { get; set; } = ProjectStructureAgentCapability.All;

    public int AutoApproveMinutes { get; set; }

    public int ApprovalRequiredMinutes { get; set; }

    public bool RequireApprovalForAllMutations { get; set; }

    public string Notes { get; set; } = string.Empty;
}

internal sealed class ProjectStructureAgentProjectOverrideRecordConfiguration : IEntityTypeConfiguration<ProjectStructureAgentProjectOverrideRecord>
{
    public void Configure(EntityTypeBuilder<ProjectStructureAgentProjectOverrideRecord> builder)
    {
        builder.ToTable("Workspace_ProjectStructureAgentProjectOverrides");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ProjectName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Notes).HasColumnType("TEXT");
        builder.HasIndex(item => new { item.ProfileId, item.ProjectId }).IsUnique();
    }
}

public sealed class ProjectStructureAgentWorkspaceSettingsModel
{
    public string CentralBaseUrl { get; set; } = "https://localhost:7271";

    public string InstallScriptPath { get; set; } = @"tools\Install-CanDoItAllProjectStructureMcp.ps1";

    public string SetupReadmePath { get; set; } = @"docs\project-structure-mcp-setup.md";

    public int DefaultAutoApproveMinutes { get; set; }

    public int DefaultApprovalRequiredMinutes { get; set; }
}

public sealed record ProjectStructureProjectChoice(Guid Id, string Name);

public sealed record ProjectStructureAgentProfileSummary(
    Guid Id,
    string Name,
    string Description,
    bool IsEnabled,
    ProjectStructureAgentCapability CapabilityMask,
    int AutoApproveMinutes,
    int ApprovalRequiredMinutes,
    bool RequireApprovalForAllMutations,
    string TokenPreview,
    int ProjectOverrideCount,
    DateTimeOffset UpdatedAtUtc);

public sealed class ProjectStructureAgentProfileEditorModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public ProjectStructureAgentCapability CapabilityMask { get; set; } = ProjectStructureAgentCapability.All;

    public int AutoApproveMinutes { get; set; }

    public int ApprovalRequiredMinutes { get; set; }

    public bool RequireApprovalForAllMutations { get; set; }

    public string TokenValue { get; set; } = string.Empty;

    public bool GenerateNewToken { get; set; } = true;

    public string Notes { get; set; } = string.Empty;

    public List<ProjectStructureAgentProjectOverrideEditorModel> ProjectOverrides { get; set; } = [];
}

public sealed class ProjectStructureAgentProjectOverrideEditorModel
{
    public Guid? Id { get; set; }

    public Guid ProjectId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public ProjectStructureAgentCapability CapabilityMask { get; set; } = ProjectStructureAgentCapability.All;

    public int AutoApproveMinutes { get; set; }

    public int ApprovalRequiredMinutes { get; set; }

    public bool RequireApprovalForAllMutations { get; set; }

    public string Notes { get; set; } = string.Empty;
}

public sealed record ProjectStructureAgentSetupGuide(
    string BaseUrl,
    string SettingsFilePath,
    string SettingsJson,
    string CodexConfigSnippet,
    string PowerShellCommand,
    string ReadmePath,
    string TokenValue,
    string TokenUsageNote);

public sealed record ProjectStructureEffectivePolicy(
    Guid ProfileId,
    string ProfileName,
    Guid? ProjectId,
    string PolicySource,
    bool IsEnabled,
    ProjectStructureAgentCapability CapabilityMask,
    int AutoApproveMinutes,
    int ApprovalRequiredMinutes,
    bool RequireApprovalForAllMutations);

public sealed record ProjectStructureAuthorizationDecision(
    Guid ProfileId,
    string ProfileName,
    ProjectStructureEffectivePolicy Policy);

public sealed class ProjectStructureAuthorizationException : Exception
{
    public ProjectStructureAuthorizationException(int statusCode, string errorCode, string message, object? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Details = details;
    }

    public int StatusCode { get; }

    public string ErrorCode { get; }

    public object? Details { get; }
}
