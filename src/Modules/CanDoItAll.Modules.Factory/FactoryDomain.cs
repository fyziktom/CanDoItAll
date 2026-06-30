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

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public PromptBlockKind BlockKind { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool IsRecommendedByDefault { get; set; }

    public string PromptTypeRules { get; set; } = string.Empty;

    public string BlueprintRules { get; set; } = string.Empty;

    public string PhaseRules { get; set; } = string.Empty;

    public string GroupKey { get; set; } = string.Empty;

    public string TagsJson { get; set; } = "[]";

    public string StackTagsJson { get; set; } = "[]";

    public string TemplateTokensJson { get; set; } = "[]";

    public bool ToolboxEligible { get; set; }

    public int OrderIndex { get; set; }

    public string CatalogSource { get; set; } = string.Empty;
}

public sealed class PromptFlowTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string BlockIdsJson { get; set; } = "[]";

    public string BlockKeysJson { get; set; } = "[]";

    public string AgentSequenceJson { get; set; } = "[]";

    public string PromptTypeRules { get; set; } = string.Empty;

    public int OrderIndex { get; set; }

    public string CatalogSource { get; set; } = string.Empty;
}

public sealed class PromptBlueprint
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string PromptType { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Guidance { get; set; } = string.Empty;

    public Guid? RecommendedFlowTemplateId { get; set; }

    public string RecommendedFlowKey { get; set; } = string.Empty;

    public string RecommendedBlockKeysJson { get; set; } = "[]";

    public int OrderIndex { get; set; }

    public string CatalogSource { get; set; } = string.Empty;
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

    public Guid? ParentPromptRunNodeId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string BranchKey { get; set; } = "main";

    public string BranchLabel { get; set; } = "Main";

    public int Sequence { get; set; }

    public PromptRunNodeState State { get; set; } = PromptRunNodeState.Pending;

    public string Notes { get; set; } = string.Empty;
}

public sealed class PromptBuildSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public string Phase { get; set; } = string.Empty;

    public Guid? BlueprintId { get; set; }

    public Guid? FlowTemplateId { get; set; }

    public Guid? ProviderProfileId { get; set; }

    public Guid? PromptArtifactId { get; set; }

    public Guid? PromptRunId { get; set; }

    public Guid? SelectedPromptRunNodeId { get; set; }

    public string RepositoryName { get; set; } = string.Empty;

    public string BranchName { get; set; } = string.Empty;

    public string CommitSha { get; set; } = string.Empty;

    public string SelectedBlockIdsJson { get; set; } = "[]";

    public string SelectedResourceIdsJson { get; set; } = "[]";

    public string GeneratedPrompt { get; set; } = string.Empty;

    public string WarningSummary { get; set; } = string.Empty;

    public string CanvasUiStateJson { get; set; } = "{}";

    public string ComponentCustomizationsJson { get; set; } = "[]";

    public string SessionAttachmentsJson { get; set; } = "[]";

    public int WizardStepIndex { get; set; }

    public bool HasCustomizedBlocks { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class PromptBlockDefinitionConfiguration : IEntityTypeConfiguration<PromptBlockDefinition>
{
    public void Configure(EntityTypeBuilder<PromptBlockDefinition> builder)
    {
        builder.ToTable("Factory_PromptBlocks");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Key).HasMaxLength(180);
        builder.Property(item => item.Name).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Summary).HasColumnType("TEXT");
        builder.Property(item => item.Content).HasColumnType("TEXT");
        builder.Property(item => item.PromptTypeRules).HasMaxLength(300);
        builder.Property(item => item.BlueprintRules).HasMaxLength(300);
        builder.Property(item => item.PhaseRules).HasMaxLength(300);
        builder.Property(item => item.GroupKey).HasMaxLength(120);
        builder.Property(item => item.TagsJson).HasColumnType("TEXT");
        builder.Property(item => item.StackTagsJson).HasColumnType("TEXT");
        builder.Property(item => item.TemplateTokensJson).HasColumnType("TEXT");
        builder.Property(item => item.CatalogSource).HasMaxLength(80);
    }
}

internal sealed class PromptFlowTemplateConfiguration : IEntityTypeConfiguration<PromptFlowTemplate>
{
    public void Configure(EntityTypeBuilder<PromptFlowTemplate> builder)
    {
        builder.ToTable("Factory_PromptFlowTemplates");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Key).HasMaxLength(180);
        builder.Property(item => item.Name).HasMaxLength(160).IsRequired();
        builder.Property(item => item.Summary).HasColumnType("TEXT");
        builder.Property(item => item.BlockIdsJson).HasColumnType("TEXT");
        builder.Property(item => item.BlockKeysJson).HasColumnType("TEXT");
        builder.Property(item => item.AgentSequenceJson).HasColumnType("TEXT");
        builder.Property(item => item.PromptTypeRules).HasMaxLength(300);
        builder.Property(item => item.CatalogSource).HasMaxLength(80);
    }
}

internal sealed class PromptBlueprintConfiguration : IEntityTypeConfiguration<PromptBlueprint>
{
    public void Configure(EntityTypeBuilder<PromptBlueprint> builder)
    {
        builder.ToTable("Factory_PromptBlueprints");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Key).HasMaxLength(180);
        builder.Property(item => item.Name).HasMaxLength(160).IsRequired();
        builder.Property(item => item.PromptType).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Summary).HasColumnType("TEXT");
        builder.Property(item => item.Guidance).HasColumnType("TEXT");
        builder.Property(item => item.RecommendedFlowKey).HasMaxLength(180);
        builder.Property(item => item.RecommendedBlockKeysJson).HasColumnType("TEXT");
        builder.Property(item => item.CatalogSource).HasMaxLength(80);
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
        builder.Property(item => item.BranchLabel).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Notes).HasColumnType("TEXT");
    }
}

internal sealed class PromptBuildSessionConfiguration : IEntityTypeConfiguration<PromptBuildSession>
{
    public void Configure(EntityTypeBuilder<PromptBuildSession> builder)
    {
        builder.ToTable("Factory_PromptBuildSessions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Phase).HasMaxLength(120);
        builder.Property(item => item.RepositoryName).HasMaxLength(200);
        builder.Property(item => item.BranchName).HasMaxLength(120);
        builder.Property(item => item.CommitSha).HasMaxLength(80);
        builder.Property(item => item.SelectedBlockIdsJson).HasColumnType("TEXT");
        builder.Property(item => item.SelectedResourceIdsJson).HasColumnType("TEXT");
        builder.Property(item => item.GeneratedPrompt).HasColumnType("TEXT");
        builder.Property(item => item.WarningSummary).HasColumnType("TEXT");
        builder.Property(item => item.CanvasUiStateJson).HasColumnType("TEXT");
        builder.Property(item => item.ComponentCustomizationsJson).HasColumnType("TEXT");
        builder.Property(item => item.SessionAttachmentsJson).HasColumnType("TEXT");
    }
}

public sealed record PromptBlockSummary(
    Guid Id,
    string Key,
    string GroupKey,
    string Name,
    PromptBlockKind BlockKind,
    string Summary,
    bool IsRecommendedByDefault,
    bool ToolboxEligible,
    IReadOnlyList<string> PromptTypes,
    IReadOnlyList<string> Blueprints,
    IReadOnlyList<string> Phases,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> StackTags,
    IReadOnlyList<string> TemplateTokens,
    string Content,
    string ContentPreview,
    int OrderIndex,
    string CatalogSource);

public sealed record PromptFlowTemplateSummary(
    Guid Id,
    string Key,
    string Name,
    string Summary,
    IReadOnlyList<Guid> BlockIds,
    IReadOnlyList<string> BlockKeys,
    IReadOnlyList<string> RecommendedPromptTypes,
    IReadOnlyList<PromptFlowAgentSummary> AgentSequence,
    int OrderIndex,
    string CatalogSource);

public sealed record PromptBlueprintSummary(
    Guid Id,
    string Key,
    string Name,
    string PromptType,
    string Summary,
    string Guidance,
    Guid? RecommendedFlowTemplateId,
    string RecommendedFlowKey,
    IReadOnlyList<string> RecommendedBlockKeys,
    int OrderIndex,
    string CatalogSource);

public sealed record PromptFlowAgentSummary(
    int Order,
    Guid RoleComponentId,
    string RoleComponentKey,
    string BlueprintKey,
    string Phase,
    string Goal,
    IReadOnlyList<string> BlockKeys);

public sealed record PromptRunNodeSummary(
    Guid Id,
    string Title,
    string BranchKey,
    string BranchLabel,
    int Sequence,
    PromptRunNodeState State,
    Guid? PromptArtifactId,
    Guid? ParentNodeId,
    Guid? PromptBlockDefinitionId,
    string Notes);

public sealed class PromptBlockEditorModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public PromptBlockKind BlockKind { get; set; } = PromptBlockKind.Instruction;

    public string Summary { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool IsRecommendedByDefault { get; set; }

    public string PromptTypes { get; set; } = string.Empty;

    public string Blueprints { get; set; } = string.Empty;

    public string Phases { get; set; } = string.Empty;
}

public sealed class PromptFlowTemplateEditorModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public List<Guid> SelectedBlockIds { get; set; } = [];

    public string RecommendedPromptTypes { get; set; } = string.Empty;
}

public sealed class PromptSessionComponentCustomization
{
    public Guid BlockId { get; set; }

    public string BlockKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string RenderedContent { get; set; } = string.Empty;

    public List<PromptTemplateValue> TemplateValues { get; set; } = [];
}

public sealed class PromptTemplateValue
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public sealed class PromptSessionAttachmentSummary
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Kind { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string LinkUrl { get; set; } = string.Empty;

    public string MediaRelativePath { get; set; } = string.Empty;

    public string MediaRoute { get; set; } = string.Empty;

    public string MediaContentType { get; set; } = string.Empty;

    public string MediaOriginalFileName { get; set; } = string.Empty;

    public string StorageObjectReferenceJson { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = string.Empty;
}

public sealed class PromptSessionSetupProfile
{
    public string IntentCategory { get; set; } = string.Empty;

    public string MainLanguage { get; set; } = string.Empty;

    public string SecondaryLanguages { get; set; } = string.Empty;

    public string ApplicationState { get; set; } = string.Empty;

    public string WorkRepository { get; set; } = string.Empty;

    public string SourceRepositories { get; set; } = string.Empty;

    public string GuidanceNotes { get; set; } = string.Empty;

    public string ProjectSnapshot { get; set; } = string.Empty;
}

public sealed record PromptLibraryGroupSummary(
    string Key,
    string Name,
    string Summary,
    string Purpose,
    string UiMode,
    int Order,
    int ComponentCount,
    IReadOnlyList<PromptBlockSummary> Components);

public sealed record PromptLibraryCatalogSummary(
    IReadOnlyList<PromptLibraryGroupSummary> Groups,
    IReadOnlyList<PromptFlowTemplateSummary> FlowTemplates,
    IReadOnlyList<PromptBlueprintSummary> Blueprints,
    int ComponentCount,
    int FlowCount,
    int BlueprintCount);

public sealed class PromptFactoryEditorModel
{
    public Guid? SessionId { get; set; }

    public Guid? PromptRunId { get; set; }

    public string SessionName { get; set; } = string.Empty;

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

    public string CanvasUiStateJson { get; set; } = "{}";

    public List<PromptSessionComponentCustomization> ComponentCustomizations { get; set; } = [];

    public List<PromptSessionAttachmentSummary> SessionAttachments { get; set; } = [];

    public List<PromptRunNodeSummary> Nodes { get; set; } = [];

    public bool HasCustomizedBlocks { get; set; }

    public int WizardStepIndex { get; set; }

    public Guid? SelectedNodeId { get; set; }
}


