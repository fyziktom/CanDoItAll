using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Factory;

public enum PromptBlockKind
{
    Instruction,
    Constraint,
    Validation,
    Delivery,
    Security,
    Testing
}

public enum PromptRunNodeState
{
    Pending,
    Prepared,
    Running,
    Used,
    Skipped,
    Failed,
    Validated,
    Superseded
}

public sealed class PromptBlockDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public PromptBlockKind BlockKind { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool IsRecommendedByDefault { get; set; }
}

public sealed class PromptFlowTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string BlockIdsJson { get; set; } = "[]";
}

public sealed class PromptBlueprint
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string PromptType { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Guidance { get; set; } = string.Empty;

    public Guid? RecommendedFlowTemplateId { get; set; }
}

public sealed class PromptRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid FlowTemplateId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class PromptRunNode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PromptRunId { get; set; }

    public Guid? PromptBlockDefinitionId { get; set; }

    public Guid? PromptArtifactId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string BranchKey { get; set; } = "main";

    public int Sequence { get; set; }

    public PromptRunNodeState State { get; set; } = PromptRunNodeState.Pending;

    public string Notes { get; set; } = string.Empty;
}

public sealed class PromptBuildSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public string Phase { get; set; } = string.Empty;

    public Guid? BlueprintId { get; set; }

    public Guid? FlowTemplateId { get; set; }

    public Guid? ProviderProfileId { get; set; }

    public Guid? PromptArtifactId { get; set; }

    public Guid? PromptRunId { get; set; }

    public string RepositoryName { get; set; } = string.Empty;

    public string BranchName { get; set; } = string.Empty;

    public string CommitSha { get; set; } = string.Empty;

    public string SelectedBlockIdsJson { get; set; } = "[]";

    public string SelectedResourceIdsJson { get; set; } = "[]";

    public string GeneratedPrompt { get; set; } = string.Empty;

    public string WarningSummary { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class PromptBlockDefinitionConfiguration : IEntityTypeConfiguration<PromptBlockDefinition>
{
    public void Configure(EntityTypeBuilder<PromptBlockDefinition> builder)
    {
        builder.ToTable("Factory_PromptBlocks");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Summary).HasColumnType("TEXT");
        builder.Property(item => item.Content).HasColumnType("TEXT");
    }
}

internal sealed class PromptFlowTemplateConfiguration : IEntityTypeConfiguration<PromptFlowTemplate>
{
    public void Configure(EntityTypeBuilder<PromptFlowTemplate> builder)
    {
        builder.ToTable("Factory_PromptFlowTemplates");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Summary).HasColumnType("TEXT");
        builder.Property(item => item.BlockIdsJson).HasColumnType("TEXT");
    }
}

internal sealed class PromptBlueprintConfiguration : IEntityTypeConfiguration<PromptBlueprint>
{
    public void Configure(EntityTypeBuilder<PromptBlueprint> builder)
    {
        builder.ToTable("Factory_PromptBlueprints");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(160).IsRequired();
        builder.Property(item => item.PromptType).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Summary).HasColumnType("TEXT");
        builder.Property(item => item.Guidance).HasColumnType("TEXT");
    }
}

internal sealed class PromptRunConfiguration : IEntityTypeConfiguration<PromptRun>
{
    public void Configure(EntityTypeBuilder<PromptRun> builder)
    {
        builder.ToTable("Factory_PromptRuns");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Phase).HasMaxLength(120);
    }
}

internal sealed class PromptRunNodeConfiguration : IEntityTypeConfiguration<PromptRunNode>
{
    public void Configure(EntityTypeBuilder<PromptRunNode> builder)
    {
        builder.ToTable("Factory_PromptRunNodes");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Title).HasMaxLength(200).IsRequired();
        builder.Property(item => item.BranchKey).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Notes).HasColumnType("TEXT");
    }
}

internal sealed class PromptBuildSessionConfiguration : IEntityTypeConfiguration<PromptBuildSession>
{
    public void Configure(EntityTypeBuilder<PromptBuildSession> builder)
    {
        builder.ToTable("Factory_PromptBuildSessions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Phase).HasMaxLength(120);
        builder.Property(item => item.RepositoryName).HasMaxLength(200);
        builder.Property(item => item.BranchName).HasMaxLength(120);
        builder.Property(item => item.CommitSha).HasMaxLength(80);
        builder.Property(item => item.SelectedBlockIdsJson).HasColumnType("TEXT");
        builder.Property(item => item.SelectedResourceIdsJson).HasColumnType("TEXT");
        builder.Property(item => item.GeneratedPrompt).HasColumnType("TEXT");
        builder.Property(item => item.WarningSummary).HasColumnType("TEXT");
    }
}

public sealed record PromptBlockSummary(Guid Id, string Name, PromptBlockKind BlockKind, string Summary, bool IsRecommendedByDefault);

public sealed record PromptFlowTemplateSummary(Guid Id, string Name, string Summary, IReadOnlyList<Guid> BlockIds);

public sealed record PromptBlueprintSummary(Guid Id, string Name, string PromptType, string Summary, string Guidance, Guid? RecommendedFlowTemplateId);

public sealed record PromptRunNodeSummary(Guid Id, string Title, string BranchKey, int Sequence, PromptRunNodeState State, Guid? PromptArtifactId);

public sealed class PromptFactoryEditorModel
{
    public Guid? SessionId { get; set; }

    public Guid? ProjectId { get; set; }

    public string Phase { get; set; } = string.Empty;

    public Guid? BlueprintId { get; set; }

    public Guid? FlowTemplateId { get; set; }

    public Guid? ProviderProfileId { get; set; }

    public string RepositoryName { get; set; } = string.Empty;

    public string BranchName { get; set; } = string.Empty;

    public string CommitSha { get; set; } = string.Empty;

    public List<Guid> SelectedBlockIds { get; set; } = [];

    public List<Guid> SelectedResourceIds { get; set; } = [];

    public string GeneratedPrompt { get; set; } = string.Empty;

    public string WarningSummary { get; set; } = string.Empty;

    public string DraftTitle { get; set; } = string.Empty;

    public List<string> Warnings { get; set; } = [];

    public List<PromptRunNodeSummary> Nodes { get; set; } = [];
}
